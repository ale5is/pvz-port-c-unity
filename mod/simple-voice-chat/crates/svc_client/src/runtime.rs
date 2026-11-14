//! Simple Voice Chat client for Horizon: the glue between Horizon's native
//! hooks and the `svc_client_*` crates (clean-room, original work, MIT).
//!
//! Horizon's thin helpers call into the shared [`Glue`] below; everything
//! testable is here and unit-tested. Not a copy of Matdoesteching's Simple
//! Voice Chat: it implements the same documented wire protocol.

use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::{Mutex, OnceLock};
use std::time::{Duration, Instant};

const SECRET_TIMEOUT: Duration = Duration::from_secs(12);

use crate::protocol as proto;
use crate::session::{Event, Session};

pub const PORT: &str = "simple-voice-chat";

pub fn is_voice_command(command: &str) -> bool {
    let command = command.trim();
    let command = command.strip_prefix('/').unwrap_or(command);
    matches!(command.split_whitespace().next(), Some("svc" | "vcgroup"))
}

/// Client settings, persisted at `<config>/simple_voice_chat/config.json`.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
#[serde(default)]
pub struct VoiceConfig {
    /// The key that opens the mic while held (`KeyV`).
    pub ptt_key: String,
    pub mute_key: String,
    pub mute_players_key: String,
    pub mute_all_key: String,
    pub enabled: bool,
    pub muted: bool,
    pub muted_players: bool,
    /// Master playback volume, 0..=1.
    pub volume: f32,
    /// Microphone capture gain, 0..=1.
    pub mic_volume: f32,
}

impl Default for VoiceConfig {
    fn default() -> Self {
        Self { ptt_key: "KeyV".into(), mute_key: "KeyM".into(), mute_players_key: "KeyM".into(), mute_all_key: "KeyN".into(), enabled: true, muted: false, muted_players: false, volume: 1.0, mic_volume: 1.0 }
    }
}

/// The mod's whole state, shared by the helpers behind a mutex.
pub struct Glue {
    pub cfg: VoiceConfig,
    pub session: Option<Session>,
    pub ptt_held: bool,
    /// uuid by player name, from the server's state packets.
    pub uuid_by_name: HashMap<String, u128>,
    pub name_by_uuid: HashMap<u128, String>,
    pub group_id: Option<u128>,
    pub group_name: Option<String>,
    pub server_addr: Option<SocketAddr>,
    /// Lines for the chat box: group joins, players entering or leaving voice.
    pub notes: Vec<String>,
    /// Set when the UDP connection dropped; the frame helper re-handshakes.
    pub reconnect_pending: bool,
    /// Mod-channel packets waiting for the frame helper to send.
    pub pending_sends: Vec<(String, Vec<u8>)>,
    pub net_tx_count: u64,
    pub net_rx_count: u64,
    pub last_tx_channel: String,
    pub last_rx_channel: String,
    pub last_tx_bytes: usize,
    pub last_rx_bytes: usize,
    pub connection_stage: String,
    pub last_net_error: String,
    /// Bounded diagnostic history of Minecraft custom payload traffic.
    pub net_history: Vec<String>,
    secret_sent_at: Option<Instant>,
    /// Live microphone capture; None until start_audio succeeds.
    pub mic: Option<crate::audio_io::MicStream>,
    /// Live speaker output; None until start_audio succeeds.
    pub speaker: Option<crate::audio_io::Speaker>,
    /// The Opus codec, created with the audio devices.
    pub codec: Option<crate::audio::VoiceCodec>,
    last_audio_attempt: Option<Instant>,
    pub audio_permission_granted: bool,
    pub permission_checked: bool,
    pub permission_check_error: bool,
    pub hud_open: bool,
    last_permission_check: Option<Instant>,
    world_active: bool,
}

impl Default for Glue {
    fn default() -> Self {
        Self {
            cfg: VoiceConfig::default(),
            session: None,
            ptt_held: false,
            uuid_by_name: HashMap::new(),
            name_by_uuid: HashMap::new(),
            group_id: None,
            group_name: None,
            server_addr: None,
            notes: Vec::new(),
            reconnect_pending: false,
            pending_sends: Vec::new(),
            net_tx_count: 0,
            net_rx_count: 0,
            last_tx_channel: "-".into(),
            last_rx_channel: "-".into(),
            last_tx_bytes: 0,
            last_rx_bytes: 0,
            connection_stage: "SIN INICIAR".into(),
            last_net_error: String::new(),
            net_history: Vec::new(),
            secret_sent_at: None,
            mic: None,
            speaker: None,
            codec: None,
            last_audio_attempt: None,
            audio_permission_granted: false,
            permission_checked: false,
            permission_check_error: false,
            hud_open: false,
            last_permission_check: None,
            world_active: false,
        }
    }
}

impl Glue {
    fn push_net_event(&mut self, event: String) {
        const MAX_NET_HISTORY: usize = 8;
        if self.net_history.len() == MAX_NET_HISTORY {
            self.net_history.remove(0);
        }
        self.net_history.push(event);
    }

    pub fn config_path(base: &str) -> String {
        format!("{}/simple_voice_chat/config.json", base)
    }

    pub fn load_config(base: &str) -> VoiceConfig {
        std::fs::read_to_string(Self::config_path(base))
            .ok()
            .and_then(|t| serde_json::from_str(&t).ok())
            .unwrap_or_default()
    }

    pub fn save_config(base: &str, cfg: &VoiceConfig) {
        let _ = std::fs::create_dir_all(format!("{}/simple_voice_chat", base));
        if let Ok(t) = serde_json::to_string_pretty(cfg) {
            let _ = std::fs::write(Self::config_path(base), t);
        }
    }

    /// A world opened: drop any old session, the handshake goes out next.
    pub fn world_open(&mut self) {
        self.world_active = true;
        self.permission_checked = false;
        self.permission_check_error = false;
        self.last_permission_check = None;
        self.last_audio_attempt = None;
        self.session = None;
        self.server_addr = None;
        self.group_id = None;
        self.group_name = None;
        self.ptt_held = false;
        self.reconnect_pending = false;
        self.notes.clear();
        self.pending_sends.clear();
        self.net_tx_count = 0;
        self.net_rx_count = 0;
        self.last_tx_channel = "-".into();
        self.last_rx_channel = "-".into();
        self.last_tx_bytes = 0;
        self.last_rx_bytes = 0;
        self.connection_stage = "SOLICITUD EN COLA".into();
        self.last_net_error.clear();
        self.net_history.clear();
        self.secret_sent_at = None;
    }

    /// The world closed: the socket dies with it.
    pub fn world_close(&mut self) {
        self.world_active = false;
        self.hud_open = false;
        if let Some(mut s) = self.session.take() {
            s.close();
        }
        self.mic = None;
        self.speaker = None;
        self.codec = None;
        self.last_audio_attempt = None;
        self.pending_sends.clear();
        self.reconnect_pending = false;
        self.secret_sent_at = None;
    }

    pub fn is_world_active(&self) -> bool {
        self.world_active
    }

    /// Mute by releasing the capture stream, not merely discarding encoded audio.
    /// Playback and the UDP voice session remain active.
    pub fn set_muted(&mut self, muted: bool) {
        self.cfg.muted = muted;
        if muted {
            self.ptt_held = false;
            self.mic = None;
        } else {
            self.last_audio_attempt = None;
        }
    }

    pub fn set_players_muted(&mut self, muted: bool) {
        self.cfg.muted_players = muted;
        if muted {
            self.speaker = None;
        } else {
            self.last_audio_attempt = None;
        }
    }

    pub fn set_all_muted(&mut self, muted: bool) {
        self.set_muted(muted);
        self.set_players_muted(muted);
    }

    /// The handshake request: the channel and body for `native_net_send_to_server`.
    pub fn handshake() -> (String, Vec<u8>) {
        (proto::REQUEST_SECRET.to_string(), proto::request_secret())
    }

    /// Record a custom payload after it has been handed to Horizon's packet sender.
    pub fn record_tx(&mut self, channel: &str, bytes: usize, now: Instant) {
        self.net_tx_count = self.net_tx_count.saturating_add(1);
        self.last_tx_channel = channel.to_string();
        self.last_tx_bytes = bytes;
        self.push_net_event(format!("TX {} ({} B)", channel, bytes));
        if channel == proto::REQUEST_SECRET {
            self.connection_stage = "SOLICITUD ENVIADA".into();
            self.last_net_error.clear();
            self.secret_sent_at = Some(now);
        }
    }

    /// A message arrived from the server on one of our channels.
    pub fn on_net(&mut self, channel: &str, data: &[u8], hint: Option<&str>, now: Instant) {
        self.net_rx_count = self.net_rx_count.saturating_add(1);
        self.last_rx_channel = channel.to_string();
        self.last_rx_bytes = data.len();
        self.push_net_event(format!("RX {} ({} B)", channel, data.len()));
        let parsed = match proto::read(channel, data) {
            Some(Ok(message)) => message,
            Some(Err(_)) => {
                self.connection_stage = "PAYLOAD INVALIDO".into();
                self.last_net_error = format!("{}: datos malformados ({} B)", channel, data.len());
                self.push_net_event(format!("RX INVALIDO {}", channel));
                return;
            }
            None => {
                self.connection_stage = "CANAL NO RECONOCIDO".into();
                self.last_net_error = format!("canal no soportado: {}", channel);
                self.push_net_event(format!("RX SIN SOPORTE {}", channel));
                return;
            }
        };
        self.last_net_error.clear();
        self.push_net_event(format!("RX PARSE OK {}", channel));
        match parsed {
            proto::ServerMessage::Secret { secret, port, player, distance, keep_alive_ms, voice_host, .. } => {
                self.secret_sent_at = None;
                self.connection_stage = "SECRETO RECIBIDO".into();
                let host = if voice_host.is_empty() {
                    let Some(address) = hint.and_then(server_host) else {
                        self.connection_stage = "ERROR DIRECCION SERVIDOR".into();
                        self.last_net_error = "no se pudo resolver la direccion del servidor Minecraft".into();
                        self.notes.push("[voice] could not determine the game server address".into());
                        return;
                    };
                    address
                } else {
                    resolve_host(&voice_host)
                };
                let target = match socket_of(&host, port) {
                    Ok(address) => address,
                    Err(error) => {
                        self.connection_stage = "ERROR DNS UDP".into();
                        self.last_net_error = format!("no se pudo resolver servidor UDP: {error}");
                        self.notes.push(format!("[voice] could not resolve voice server: {error}"));
                        return;
                    }
                };
                self.server_addr = Some(target);
                let mut s = Session::new(player);
                s.set_max_distance(distance);
                s.timeout_after = Duration::from_millis((keep_alive_ms.clamp(100, 60_000) as u64).saturating_mul(10));
                if let Err(error) = s.on_secret(secret, target) {
                    self.connection_stage = "ERROR ABRIR UDP".into();
                    self.last_net_error = format!("no se pudo abrir UDP: {error}");
                    self.notes.push(format!("[voice] could not open UDP session: {error}"));
                    return;
                }
                self.connection_stage = "AUTH UDP ENVIADA".into();
                self.session = Some(s);
            }
            proto::ServerMessage::State(st) => self.see_state(&st, now),
            proto::ServerMessage::States(all) => {
                for st in &all {
                    self.see_state(st, now);
                }
            }
            proto::ServerMessage::RemoveState(uuid) => {
                if let Some(name) = self.name_by_uuid.remove(&uuid) {
                    self.uuid_by_name.remove(&name);
                    self.notes.push(format!("[voice] {} left voice chat", name));
                }
                if let Some(s) = &mut self.session {
                    s.forget_position(uuid);
                }
            }
            proto::ServerMessage::AddGroup { id, name, password, .. } => {
                self.notes.push(if password {
                    format!("[voice] group {} (password protected) exists", name)
                } else {
                    format!("[voice] group {} exists", name)
                });
                if self.group_id.is_none() && self.group_name.as_deref() == Some(name.as_str()) {
                    self.group_id = Some(id);
                }
            }
            proto::ServerMessage::RemoveGroup(id) => {
                if self.group_id == Some(id) {
                    self.group_id = None;
                    self.group_name = None;
                    self.notes.push("[voice] the group was removed".into());
                }
            }
            _ => {}
        }
    }

    fn see_state(&mut self, st: &proto::PlayerState, now: Instant) {
        let fresh = !self.uuid_by_name.contains_key(&st.name);
        self.uuid_by_name.insert(st.name.clone(), st.uuid);
        self.name_by_uuid.insert(st.uuid, st.name.clone());
        if fresh && !st.disconnected && self.name_by_uuid.len() > 1 {
            self.notes.push(format!("[voice] {} is in voice chat", st.name));
        }
        let _ = now;
    }

    /// Open playback and codec; open the microphone only after permission is granted.
    pub fn start_audio(&mut self, allow_microphone: bool) -> Vec<String> {
        let mut notes = Vec::new();
        if allow_microphone && self.mic.is_none() {
            match crate::audio_io::MicStream::open() {
                Ok(m) => self.mic = Some(m),
                Err(e) => notes.push(format!("[voice] no microphone: {}", e)),
            }
        }
        if !self.cfg.muted_players && self.speaker.is_none() {
            match crate::audio_io::Speaker::open() {
                Ok(s) => self.speaker = Some(s),
                Err(e) => notes.push(format!("[voice] no speaker: {}", e)),
            }
        }
        if self.codec.is_none() {
            match crate::audio::VoiceCodec::new() {
                Ok(c) => self.codec = Some(c),
                Err(e) => notes.push(format!("[voice] audio codec failed to start: {}", e)),
            }
        }
        notes
    }

    pub fn set_audio_permission(&mut self, granted: bool) {
        self.permission_checked = true;
        self.permission_check_error = false;
        if self.audio_permission_granted != granted {
            self.audio_permission_granted = granted;
            self.last_audio_attempt = None;
            if !granted {
                self.mic = None;
            }
        }
    }

    pub fn set_audio_permission_error(&mut self) {
        self.permission_checked = true;
        self.permission_check_error = true;
    }

    pub fn should_check_audio_permission(&mut self, now: Instant) -> bool {
        if self.last_permission_check.is_some_and(|last| {
            now.saturating_duration_since(last) < std::time::Duration::from_secs(1)
        }) {
            return false;
        }
        self.last_permission_check = Some(now);
        true
    }

    pub fn ensure_audio(&mut self, now: Instant) -> Vec<String> {
        if !self.world_active
            || !self.session.as_ref().is_some_and(|session| {
                session.state() == crate::session::State::Connected
            })
        {
            return Vec::new();
        }
        let need_microphone = self.audio_permission_granted && !self.cfg.muted && self.mic.is_none();
        let need_output = (!self.cfg.muted_players && self.speaker.is_none()) || self.codec.is_none();
        if !need_microphone && !need_output {
            return Vec::new();
        }
        if self
            .last_audio_attempt
            .is_some_and(|last| now.saturating_duration_since(last) < std::time::Duration::from_secs(5))
        {
            return Vec::new();
        }
        self.last_audio_attempt = Some(now);
        self.start_audio(self.audio_permission_granted)
    }

    /// The microphone side: when talking, turn captured audio into Opus
    /// packets on the session; always drain the capture so it stays fresh.
    fn pump_mic(&mut self) {
        let talking = !self.cfg.muted
            && self.ptt_held
            && self
                .session
                .as_ref()
                .map(|s| s.state() == crate::session::State::Connected)
                .unwrap_or(false);
        let Some(mic) = &mut self.mic else { return };
        while let Some(frame) = mic.poll_frame() {
            if !talking {
                continue;
            }
            let mut adjusted = frame;
            for sample in &mut adjusted {
                *sample = (*sample as f32 * self.cfg.mic_volume)
                    .clamp(-32768.0, 32767.0) as i16;
            }
            let packet = match self.codec.as_mut().map(|c| c.encode(&adjusted)) {
                Some(Ok(p)) => p,
                _ => break,
            };
            if let Some(s) = &mut self.session {
                s.push_mic(&packet);
            }
        }
    }

    /// The playback side: decode voice events and play them, applying the
    /// positional volume and the user's master volume.
    fn pump_speaker(&mut self, events: &[crate::session::Event]) {
        if self.cfg.muted_players {
            return;
        }
        let Some(spk) = &self.speaker else { return };
        for e in events {
            if let crate::session::Event::Voice { data, volume, .. } = e {
                let Some(mut pcm) = self
                    .codec
                    .as_mut()
                    .and_then(|c| c.decode(Some(data)).ok())
                else {
                    continue;
                };
                crate::audio::mix(&mut pcm, volume * self.cfg.volume);
                spk.play(&pcm);
            }
        }
    }

    /// One frame: drive the session, hand back the voice events it queued.
    pub fn on_frame(&mut self, now: Instant) -> Vec<Event> {
        if self.session.is_none() {
            if self.connection_stage == "SOLICITUD ENVIADA"
                && self.secret_sent_at.is_some_and(|sent| now.saturating_duration_since(sent) >= SECRET_TIMEOUT)
            {
                self.connection_stage = "TIMEOUT SECRET".into();
                self.last_net_error = format!(
                    "no se recibio voicechat:secret despues de {} segundos",
                    SECRET_TIMEOUT.as_secs()
                );
            }
            return Vec::new();
        }
        let Some(s) = &mut self.session else { return Vec::new() };
        s.tick();
        let events = s.pump();
        for e in &events {
            match e {
                Event::Connected => {
                    self.connection_stage = "UDP CONECTADO".into();
                    self.last_net_error.clear();
                }
                Event::Dropped(reason) => {
                    self.reconnect_pending = true;
                    self.connection_stage = "UDP DESCONECTADO".into();
                    self.last_net_error = format!("conexion UDP perdida: {reason}");
                }
                Event::Voice { .. } => {}
            }
        }
        self.pump_speaker(&events);
        self.pump_mic();
        events
    }

    /// A key event: PTT on press and release, mute on the mute key.
    pub fn on_key(&mut self, code: &str, pressed: bool) -> Option<String> {
        if code.eq_ignore_ascii_case(&self.cfg.ptt_key) {
            self.ptt_held = pressed;
            return None;
        }
        if pressed && code.eq_ignore_ascii_case(&self.cfg.mute_key) {
            self.set_muted(!self.cfg.muted);
            return Some(if self.cfg.muted {
                "voice muted".into()
            } else {
                "voice unmuted".into()
            });
        }
        None
    }

    pub fn on_key_binding(
        &mut self,
        key: &str,
        pressed: bool,
        ptt_binding: &str,
        mute_players_binding: &str,
        mute_all_binding: &str,
    ) -> Option<String> {
        if key == ptt_binding {
            self.ptt_held = pressed;
            return None;
        }
        if !pressed {
            return None;
        }
        if key == mute_players_binding {
            self.set_players_muted(!self.cfg.muted_players);
            return Some(if self.cfg.muted_players {
                "other players muted".into()
            } else {
                "other players unmuted".into()
            });
        }
        if key == mute_all_binding {
            let muted = !(self.cfg.muted && self.cfg.muted_players);
            self.set_all_muted(muted);
            return Some(if muted {
                "microphone and other players muted".into()
            } else {
                "microphone and other players unmuted".into()
            });
        }
        None
    }

    /// `/svc` and `/vcgroup`: returns the chat reply and any packets to send.
    pub fn on_command(&mut self, command: &str) -> (String, Vec<(String, Vec<u8>)>) {
        let mut sends = Vec::new();
        let mut args = command.split_whitespace();
        let head = args.next().unwrap_or("");
        let sub = args.next().unwrap_or("");
        let rest: Vec<&str> = args.collect();
        if head == "vcgroup" {
            return (self.group_command(sub, &rest, &mut sends), sends);
        }
        if head != "svc" {
            return (String::new(), sends);
        }
        match sub {
            "mute" => {
                self.set_muted(true);
                ("voice muted".into(), sends)
            }
            "unmute" => {
                self.set_muted(false);
                ("voice unmuted".into(), sends)
            }
            "toggle" => {
                self.set_muted(!self.cfg.muted);
                (if self.cfg.muted { "voice muted" } else { "voice unmuted" }.into(), sends)
            }
            "muteplayers" => {
                self.set_players_muted(true);
                ("other players muted".into(), sends)
            }
            "unmuteplayers" => {
                self.set_players_muted(false);
                ("other players unmuted".into(), sends)
            }
            "muteall" => {
                self.set_all_muted(true);
                ("microphone and other players muted".into(), sends)
            }
            "unmuteall" => {
                self.set_all_muted(false);
                ("microphone and other players unmuted".into(), sends)
            }
            "ptt" => {
                if let Some(k) = rest.first() {
                    if !valid_key_code(k) {
                        return ("usage: /svc ptt KeyV (use a key code such as KeyV or KeyM)".into(), sends);
                    }
                    self.cfg.ptt_key = k.to_string();
                    (format!("push-to-talk key set to {}", k), sends)
                } else {
                    (format!("push-to-talk key is {}", self.cfg.ptt_key), sends)
                }
            }
            "volume" => {
                if let Some(value) = rest.first().and_then(|v| v.parse::<f32>().ok()) {
                    self.cfg.volume = value.clamp(0.0, 100.0) / 100.0;
                }
                (format!("voice output volume: {}%", (self.cfg.volume * 100.0).round() as u32), sends)
            }
            "micvolume" => {
                if let Some(value) = rest.first().and_then(|v| v.parse::<f32>().ok()) {
                    self.cfg.mic_volume = value.clamp(0.0, 100.0) / 100.0;
                }
                (format!("microphone gain: {}%", (self.cfg.mic_volume * 100.0).round() as u32), sends)
            }
            "status" => (
                format!(
                    "voice chat: {}, mic permission: {}, mic gain: {}%, output: {}%, PTT: {}",
                    if self.session.as_ref().is_some_and(|s| s.state() == crate::session::State::Connected) { "connected" } else { "disconnected" },
                    if self.audio_permission_granted { "granted" } else { "not granted" },
                    (self.cfg.mic_volume * 100.0).round() as u32,
                    (self.cfg.volume * 100.0).round() as u32,
                    self.cfg.ptt_key
                ) + if self.cfg.muted { ", muted" } else { ", unmuted" },
                sends,
            ),
            _ => (
                "/svc mute|unmute|toggle|ptt <KeyCode>|volume <0-100>|micvolume <0-100>|status; /vcgroup create|join|leave|list".into(),
                sends,
            ),
        }
    }

    fn group_command(&mut self, sub: &str, rest: &[&str], sends: &mut Vec<(String, Vec<u8>)>) -> String {
        match sub {
            "create" => {
                let Some(name) = rest.first() else {
                    return "usage: /vcgroup create <name>".into();
                };
                sends.push((
                    proto::CREATE_GROUP.to_string(),
                    proto::create_group(name, None, proto::GroupKind::Normal),
                ));
                self.group_name = Some(name.to_string());
                format!("creating group {}", name)
            }
            "join" => {
                let Some(id) = rest.first().and_then(|n| self.uuid_by_name.get(*n).copied()) else {
                    return "usage: /vcgroup join <group id or name>".into();
                };
                sends.push((proto::SET_GROUP.to_string(), proto::join_group(id, None)));
                format!("joining group {}", id)
            }
            "leave" => {
                sends.push((proto::LEAVE_GROUP.to_string(), proto::leave_group()));
                self.group_id = None;
                self.group_name = None;
                "left the group".into()
            }
            "list" => {
                let mut names: Vec<&String> = self.uuid_by_name.keys().collect();
                names.sort();
                format!("in voice chat: {}", names.iter().map(|s| s.as_str()).collect::<Vec<_>>().join(", "))
            }
            _ => "usage: /vcgroup create|join|leave|list".into(),
        }
    }
}

fn valid_key_code(key: &str) -> bool {
    (key.starts_with("Key") && key.len() == 4 && key.as_bytes()[3].is_ascii_alphabetic())
        || (key.starts_with("Digit") && key.len() == 6 && key.as_bytes()[5].is_ascii_digit())
}

fn host_of(addr: SocketAddr) -> String {
    match addr {
        SocketAddr::V4(v4) => v4.ip().to_string(),
        SocketAddr::V6(v6) => v6.ip().to_string(),
    }
}

fn server_host(address: &str) -> Option<String> {
    use std::net::ToSocketAddrs;
    address
        .to_socket_addrs()
        .ok()
        .and_then(|mut addresses| addresses.next())
        .map(host_of)
        .or_else(|| {
            format!("{address}:25565")
                .to_socket_addrs()
                .ok()
                .and_then(|mut addresses| addresses.next())
                .map(host_of)
        })
}

fn resolve_host(host: &str) -> String {
    use std::net::{IpAddr, ToSocketAddrs};
    if host.parse::<IpAddr>().is_ok() {
        return host.to_string();
    }
    format!("{host}:0")
        .to_socket_addrs()
        .ok()
        .and_then(|mut addresses| addresses.next())
        .map(host_of)
        .unwrap_or_else(|| host.to_string())
}

fn socket_of(host: &str, port: u16) -> Result<SocketAddr, String> {
    use std::net::{IpAddr, ToSocketAddrs};
    if let Ok(ip) = host.parse::<IpAddr>() {
        return Ok(SocketAddr::new(ip, port));
    }
    format!("{host}:{port}")
        .to_socket_addrs()
        .map_err(|error| error.to_string())?
        .next()
        .ok_or_else(|| format!("no address found for {host}"))
}

/// The shared state the helpers reach.
pub static STATE: OnceLock<Mutex<Glue>> = OnceLock::new();

pub fn state() -> &'static Mutex<Glue> {
    STATE.get_or_init(|| Mutex::new(Glue::default()))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn glue() -> Glue {
        Glue::default()
    }

    #[test]
    fn audio_requires_an_active_world_and_permission() {
        let mut glue = Glue::default();
        glue.set_audio_permission(true);
        assert!(glue.ensure_audio(Instant::now()).is_empty());
        assert!(glue.last_audio_attempt.is_none());
        glue.world_open();
        assert!(glue.is_world_active());
        assert!(glue.ensure_audio(Instant::now()).is_empty());
        assert!(glue.last_audio_attempt.is_none(), "audio must not initialize before the SVC UDP session is connected");
        glue.world_close();
        assert!(!glue.is_world_active());
        assert!(glue.mic.is_none());
        assert!(glue.speaker.is_none());
    }

    #[test]
    fn only_voice_commands_are_consumed() {
        assert!(is_voice_command("svc mute"));
        assert!(is_voice_command("/svc status"));
        assert!(is_voice_command("vcgroup list"));
        assert!(is_voice_command("/vcgroup create friends"));
        assert!(!is_voice_command("help"));
        assert!(!is_voice_command("/say hello"));
        assert!(!is_voice_command("svchack"));
        assert!(!is_voice_command("vcgroups"));
    }

    #[test]
    fn game_server_address_resolves_to_voice_host() {
        let host = server_host("127.0.0.1:25565").unwrap();
        assert_eq!(host, "127.0.0.1");
        assert_eq!(
            socket_of(&host, 24454).unwrap(),
            "127.0.0.1:24454".parse::<SocketAddr>().unwrap()
        );
    }

    #[test]
    fn voice_socket_accepts_ipv6_addresses() {
        let address = socket_of("::1", 24454).unwrap();
        assert_eq!(address, "[::1]:24454".parse::<SocketAddr>().unwrap());
    }

    use std::time::Instant;

    fn secret_bytes(player: u128, port: u16, distance: f64) -> Vec<u8> {
        let mut out = Vec::new();
        // The secret reply body: fixed secret, port, player uuid, codec, mtu,
        // distance, keep-alive, groups flag, voice host, recording flag.
        out.extend_from_slice(&[0u8; 16]);
        out.extend_from_slice(&(port as i32).to_be_bytes());
        out.extend_from_slice(&player.to_be_bytes());
        out.push(0);
        out.extend_from_slice(&1024i32.to_be_bytes());
        out.extend_from_slice(&distance.to_be_bytes());
        out.extend_from_slice(&2500i32.to_be_bytes());
        out.push(1); // groups allowed
        out.push(0); // voice host: empty
        out.push(0); // recording not allowed
        out
    }

    #[test]
    fn network_history_records_payloads_and_is_bounded() {
        let mut g = glue();
        g.world_open();
        let now = Instant::now();
        g.record_tx(proto::REQUEST_SECRET, 4, now);
        let states = 0i32.to_be_bytes();
        g.on_net(proto::STATES, &states, None, now);

        assert!(g.net_history.iter().any(|line| line == "TX voicechat:request_secret (4 B)"));
        assert!(g.net_history.iter().any(|line| line == "RX voicechat:states (4 B)"));
        assert!(g.net_history.iter().any(|line| line == "RX PARSE OK voicechat:states"));

        for index in 0..12 {
            g.record_tx(&format!("test:{index}"), index, now);
        }
        assert_eq!(g.net_history.len(), 8);
        assert!(g.net_history.first().unwrap().contains("test:4"));
        assert!(g.net_history.last().unwrap().contains("test:11"));
    }

    #[test]
    fn missing_secret_reply_is_reported_after_timeout() {
        let mut g = glue();
        g.world_open();
        let sent = Instant::now();
        g.record_tx(proto::REQUEST_SECRET, proto::request_secret().len(), sent);
        g.on_frame(sent + SECRET_TIMEOUT);
        assert_eq!(g.connection_stage, "TIMEOUT SECRET");
        assert!(g.last_net_error.contains("voicechat:secret"));
        assert_eq!(g.net_tx_count, 1);
        assert_eq!(g.net_rx_count, 0);
    }

    #[test]
    fn handshake_builds_and_connects() {
        let mut g = glue();
        g.world_open();
        let (channel, body) = Glue::handshake();
        assert_eq!(channel, "voicechat:request_secret");
        assert!(!body.is_empty());
        let body = secret_bytes(7, 24454, 48.0);
        eprintln!("parse: {:?}", proto::read("voicechat:secret", &body));
        g.on_net(
            "voicechat:secret",
            &body,
            Some("127.0.0.1:25565"),
            Instant::now(),
        );
        assert!(g.session.is_some());
        assert_eq!(g.session.as_ref().unwrap().timeout_after, Duration::from_millis(25_000));
        assert_eq!(g.server_addr.unwrap().port(), 24454);
    }

    #[test]
    fn states_build_the_name_map() {
        let mut g = glue();
        let body = {
            let mut out = Vec::new();
            // A states body: count as an int, then per player disabled,
            // disconnected, uuid, name, group.
            out.extend_from_slice(&2i32.to_be_bytes());
            for (uuid, name) in [(9u128, "a"), (10, "b")] {
                out.push(0); // not disabled
                out.push(0); // not disconnected
                out.extend_from_slice(&uuid.to_be_bytes());
                out.push(name.len() as u8);
                out.extend_from_slice(name.as_bytes());
                out.push(0); // no group
            }
            out
        };
        g.on_net("voicechat:states", &body, None, Instant::now());
        assert_eq!(g.uuid_by_name.get("a"), Some(&9));
        assert_eq!(g.name_by_uuid.get(&10), Some(&"b".to_string()));
    }

    #[test]
    fn ptt_and_mute_toggle() {
        let mut g = glue();
        assert_eq!(g.on_key("KeyV", true), None);
        assert!(g.ptt_held);
        g.on_key("KeyV", false);
        assert!(!g.ptt_held);
        assert_eq!(g.on_key("KeyM", true), Some("voice muted".into()));
        assert!(g.cfg.muted);
    }

    #[test]
    fn mute_releases_capture_without_closing_voice_session() {
        let mut g = Glue::default();
        g.ptt_held = true;
        g.set_muted(true);
        assert!(g.cfg.muted);
        assert!(!g.ptt_held);
        assert!(g.mic.is_none());
        assert!(g.session.is_none(), "muting must not close the voice session");
        g.set_muted(false);
        assert!(!g.cfg.muted);
    }

    #[test]
    fn commands_reply_and_send() {
        let mut g = glue();
        let (reply, sends) = g.on_command("svc mute");
        assert_eq!(reply, "voice muted");
        assert!(g.cfg.muted);
        let (reply, sends) = g.on_command("vcgroup create den");
        assert!(reply.contains("den"));
        assert_eq!(sends.len(), 1);
        assert_eq!(sends[0].0, proto::CREATE_GROUP);
    }

    #[test]
    fn voice_settings_commands_clamp_and_update() {
        let mut g = glue();
        let (reply, _) = g.on_command("svc volume 140");
        assert_eq!(reply, "voice output volume: 100%");
        assert_eq!(g.cfg.volume, 1.0);
        let (reply, _) = g.on_command("svc micvolume 35");
        assert_eq!(reply, "microphone gain: 35%");
        assert!((g.cfg.mic_volume - 0.35).abs() < 0.001);
        let (reply, _) = g.on_command("svc toggle");
        assert_eq!(reply, "voice muted");
        assert!(g.cfg.muted);
        let (reply, _) = g.on_command("svc ptt KeyB");
        assert_eq!(reply, "push-to-talk key set to KeyB");
        assert_eq!(g.cfg.ptt_key, "KeyB");
    }

    #[test]
    fn horizon_key_bindings_drive_push_to_talk_and_mute() {
        let mut g = Glue::default();
        assert_eq!(
            g.on_key_binding("key.keyboard.space", true, "key.keyboard.space", "key.keyboard.n"),
            None
        );
        assert!(g.ptt_held);
        g.on_key_binding("key.keyboard.space", false, "key.keyboard.space", "key.keyboard.n");
        assert!(!g.ptt_held);
        assert_eq!(
            g.on_key_binding("key.keyboard.n", true, "key.keyboard.space", "key.keyboard.n"),
            Some("voice muted".into())
        );
        assert!(g.cfg.muted);
    }

    #[test]
    fn config_round_trip() {
        let dir = std::env::temp_dir().join(format!("svc-test-{}", std::process::id()));
        let cfg = VoiceConfig { ptt_key: "KeyB".into(), mute_key: "KeyN".into(), enabled: false, muted: true, volume: 0.5, mic_volume: 0.75 };
        Glue::save_config(dir.to_str().unwrap(), &cfg);
        let loaded = Glue::load_config(dir.to_str().unwrap());
        assert_eq!(loaded.ptt_key, "KeyB");
        assert_eq!(loaded.mute_key, "KeyN");
        assert!(!loaded.enabled);
        assert!(loaded.muted);
        assert_eq!(loaded.volume, 0.5);
        assert_eq!(loaded.mic_volume, 0.75);
        let _ = std::fs::remove_dir_all(&dir);
    }
}
