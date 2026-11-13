//! Simple Voice Chat client for Horizon: the glue between Horizon's native
//! hooks and the `svc_client_*` crates (clean-room, original work, MIT).
//!
//! Horizon's thin helpers call into the shared [`Glue`] below; everything
//! testable is here and unit-tested. Not a copy of Matdoesteching's Simple
//! Voice Chat: it implements the same documented wire protocol.

use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::{Mutex, OnceLock};
use std::time::Instant;

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
    pub muted: bool,
    /// Master playback volume, 0..=1.
    pub volume: f32,
    /// Microphone capture gain, 0..=1.
    pub mic_volume: f32,
}

impl Default for VoiceConfig {
    fn default() -> Self {
        Self { ptt_key: "KeyV".into(), muted: false, volume: 1.0, mic_volume: 1.0 }
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
    }

    pub fn is_world_active(&self) -> bool {
        self.world_active
    }

    /// The handshake request: the channel and body for `native_net_send_to_server`.
    pub fn handshake() -> (String, Vec<u8>) {
        (proto::REQUEST_SECRET.to_string(), proto::request_secret())
    }

    /// A message arrived from the server on one of our channels.
    pub fn on_net(&mut self, channel: &str, data: &[u8], hint: Option<&str>, now: Instant) {
        let Some(parsed) = proto::read(channel, data) else { return };
        let Ok(msg) = parsed else { return };
        match msg {
            proto::ServerMessage::Secret { secret, port, player, distance, voice_host, .. } => {
                let host = if voice_host.is_empty() {
                    let Some(address) = hint.and_then(server_host) else {
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
                        self.notes.push(format!("[voice] could not resolve voice server: {error}"));
                        return;
                    }
                };
                self.server_addr = Some(target);
                let mut s = Session::new(player);
                s.set_max_distance(distance);
                if let Err(error) = s.on_secret(secret, target) {
                    self.notes.push(format!("[voice] could not open UDP session: {error}"));
                    return;
                }
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
        if self.speaker.is_none() {
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
        if !self.world_active {
            return Vec::new();
        }
        let need_microphone = self.audio_permission_granted && self.mic.is_none();
        let need_output = self.speaker.is_none() || self.codec.is_none();
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
            let adjusted: Vec<i16> = frame
                .iter()
                .map(|sample| (*sample as f32 * self.cfg.mic_volume).clamp(-32768.0, 32767.0) as i16)
                .collect();
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
    pub fn on_frame(&mut self, _now: Instant) -> Vec<Event> {
        let Some(s) = &mut self.session else { return Vec::new() };
        s.tick();
        let events = s.pump();
        for e in &events {
            if let Event::Dropped(_) = e {
                self.reconnect_pending = true;
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
        if pressed && code.eq_ignore_ascii_case("KeyM") {
            self.cfg.muted = !self.cfg.muted;
            return Some(if self.cfg.muted {
                "voice muted".into()
            } else {
                "voice unmuted".into()
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
                self.cfg.muted = true;
                ("voice muted".into(), sends)
            }
            "unmute" => {
                self.cfg.muted = false;
                ("voice unmuted".into(), sends)
            }
            "toggle" => {
                self.cfg.muted = !self.cfg.muted;
                (if self.cfg.muted { "voice muted" } else { "voice unmuted" }.into(), sends)
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
        out.extend_from_slice(&1000i32.to_be_bytes());
        out.push(1); // groups allowed
        out.push(0); // voice host: empty
        out.push(0); // recording not allowed
        out
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
    fn config_round_trip() {
        let dir = std::env::temp_dir().join(format!("svc-test-{}", std::process::id()));
        let cfg = VoiceConfig { ptt_key: "KeyB".into(), muted: true, volume: 0.5, mic_volume: 0.75 };
        Glue::save_config(dir.to_str().unwrap(), &cfg);
        let loaded = Glue::load_config(dir.to_str().unwrap());
        assert_eq!(loaded.ptt_key, "KeyB");
        assert!(loaded.muted);
        assert_eq!(loaded.volume, 0.5);
        assert_eq!(loaded.mic_volume, 0.75);
        let _ = std::fs::remove_dir_all(&dir);
    }
}
