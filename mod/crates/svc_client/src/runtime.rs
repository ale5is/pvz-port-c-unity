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

/// Client settings, persisted at `<config>/simple_voice_chat/config.json`.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
#[serde(default)]
pub struct VoiceConfig {
    pub ptt_key: String,
    pub mute_key: String,
    pub mute_players_key: String,
    pub mute_all_key: String,
    pub muted: bool,
    pub muted_players: bool,
    pub push_to_talk: bool,
    pub volume: f32,
    pub mic_volume: f32,
}

impl Default for VoiceConfig {
    fn default() -> Self {
        Self {
            ptt_key: "KeyV".into(),
            mute_key: "KeyM".into(),
            mute_players_key: "KeyB".into(),
            mute_all_key: "KeyN".into(),
            muted: false,
            muted_players: false,
            push_to_talk: true,
            volume: 1.0,
            mic_volume: 1.0,
        }
    }
}

/// The mod's whole state, shared by the helpers behind a mutex.
pub struct Glue {
    pub cfg: VoiceConfig,
    pub session: Option<Session>,
    pub ptt_held: bool,
    /// Whether the previous microphone frame started a speaking stream.
    pub mic_talking: bool,
    /// uuid by player name, from the server's state packets.
    pub uuid_by_name: HashMap<String, u128>,
    pub name_by_uuid: HashMap<u128, String>,
    pub group_id: Option<u128>,
    pub group_name: Option<String>,
    pub group_by_name: HashMap<String, u128>,
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
    /// Independent Opus decoder state for each remote voice sender.
    pub decoders: HashMap<u128, crate::audio::VoiceDecoder>,
}

impl Default for Glue {
    fn default() -> Self {
        Self {
            cfg: VoiceConfig::default(),
            session: None,
            ptt_held: false,
            mic_talking: false,
            uuid_by_name: HashMap::new(),
            name_by_uuid: HashMap::new(),
            group_id: None,
            group_name: None,
            group_by_name: HashMap::new(),
            server_addr: None,
            notes: Vec::new(),
            reconnect_pending: false,
            pending_sends: Vec::new(),
            mic: None,
            speaker: None,
            codec: None,
            decoders: HashMap::new(),
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

    pub fn set_muted(&mut self, muted: bool) {
        self.cfg.muted = muted;
        if muted {
            self.ptt_held = false;
            self.mic = None;
        } else {
            for note in self.start_audio() {
                eprintln!("{note}");
            }
        }
    }

    pub fn set_players_muted(&mut self, muted: bool) {
        self.cfg.muted_players = muted;
        if muted {
            self.speaker = None;
        } else {
            for note in self.start_audio() {
                eprintln!("{note}");
            }
        }
    }

    pub fn set_all_muted(&mut self, muted: bool) {
        self.set_muted(muted);
        self.set_players_muted(muted);
    }

    pub fn set_push_to_talk(&mut self, enabled: bool) {
        self.cfg.push_to_talk = enabled;
        self.ptt_held = false;
    }

    /// A world opened: drop any old session, the handshake goes out next.
    pub fn world_open(&mut self) {
        self.session = None;
        self.server_addr = None;
        self.group_id = None;
        self.group_name = None;
        self.group_by_name.clear();
        self.uuid_by_name.clear();
        self.name_by_uuid.clear();
        self.decoders.clear();
        self.ptt_held = false;
        self.mic_talking = false;
        self.reconnect_pending = false;
        self.notes.clear();
        self.pending_sends.clear();
    }

    /// The world closed: the socket dies with it.
    pub fn world_close(&mut self) {
        if let Some(mut s) = self.session.take() {
            s.close();
        }
        self.mic_talking = false;
        self.decoders.clear();
        self.reconnect_pending = false;
    }

    /// The handshake request: the channel and body for `native_net_send_to_server`.
    pub fn handshake() -> (String, Vec<u8>) {
        (proto::REQUEST_SECRET.to_string(), proto::request_secret())
    }

    /// A message arrived from the server on one of our channels.
    pub fn on_net(&mut self, channel: &str, data: &[u8], hint: Option<&str>, now: Instant) {
        let Some(parsed) = proto::read(channel, data) else {
            eprintln!("[PavoHorizon/SVC] IGNORE channel={} bytes={}", channel, data.len());
            return;
        };
        let msg = match parsed {
            Ok(msg) => msg,
            Err(_) => {
                eprintln!("[PavoHorizon/SVC] PARSE_ERROR channel={} bytes={}", channel, data.len());
                return;
            }
        };
        match msg {
            proto::ServerMessage::Secret { secret, port, player, distance, voice_host, .. } => {
                self.decoders.clear();
                let target = voice_socket(&voice_host, hint, port);
                self.server_addr = Some(target);
                let mut s = Session::new(player);
                s.set_max_distance(distance);
                match s.on_secret(secret, target) {
                    Ok(()) => eprintln!("[PavoHorizon/SVC] SECRET_OK udp_target={}", target),
                    Err(error) => eprintln!("[PavoHorizon/SVC] UDP_BIND_ERROR target={} error={}", target, error),
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
                    if self.uuid_by_name.get(&name) == Some(&uuid) {
                        self.uuid_by_name.remove(&name);
                    }
                    self.notes.push(format!("[voice] {} left voice chat", name));
                }
                if let Some(s) = &mut self.session {
                    s.forget_position(uuid);
                }
                self.decoders.remove(&uuid);
            }
            proto::ServerMessage::AddGroup { id, name, password, .. } => {
                self.group_by_name.insert(name.clone(), id);
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
                self.group_by_name.retain(|_, group_id| *group_id != id);
                if self.group_id == Some(id) {
                    self.group_id = None;
                    self.group_name = None;
                    self.notes.push("[voice] the group was removed".into());
                }
            }
            proto::ServerMessage::JoinedGroup { group, wrong_password } => {
                self.group_id = group;
                self.group_name = group.and_then(|id| {
                    self.group_by_name.iter().find_map(|(name, group_id)| {
                        (*group_id == id).then(|| name.clone())
                    })
                });
                self.notes.push(if wrong_password {
                    "[voice] incorrect group password".into()
                } else if self.group_id.is_some() {
                    "[voice] joined voice group".into()
                } else {
                    "[voice] left voice group".into()
                });
            }
            _ => {}
        }
    }

    fn see_state(&mut self, st: &proto::PlayerState, now: Instant) {
        let fresh = !self.uuid_by_name.contains_key(&st.name);
        if let Some(old_uuid) = self.uuid_by_name.insert(st.name.clone(), st.uuid) {
            if old_uuid != st.uuid {
                self.name_by_uuid.remove(&old_uuid);
                self.decoders.remove(&old_uuid);
                if let Some(session) = &mut self.session {
                    session.forget_position(old_uuid);
                }
            }
        }
        if let Some(old_name) = self.name_by_uuid.insert(st.uuid, st.name.clone()) {
            if old_name != st.name && self.uuid_by_name.get(&old_name) == Some(&st.uuid) {
                self.uuid_by_name.remove(&old_name);
            }
        }
        if fresh && !st.disconnected && self.name_by_uuid.len() > 1 {
            self.notes.push(format!("[voice] {} is in voice chat", st.name));
        }
        let _ = now;
    }

    /// Open the microphone and speaker. Returns chat notes for what failed;
    /// called once per client start, devices outlive worlds.
    pub fn start_audio(&mut self) -> Vec<String> {
        let mut notes = Vec::new();
        if !self.cfg.muted && self.mic.is_none() {
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
        if (!self.cfg.muted || !self.cfg.muted_players) && self.codec.is_none() {
            match crate::audio::VoiceCodec::new() {
                Ok(c) => self.codec = Some(c),
                Err(e) => notes.push(format!("[voice] audio codec failed to start: {}", e)),
            }
        }
        notes
    }

    /// The microphone side: when talking, turn captured audio into Opus
    /// packets on the session; always drain the capture so it stays fresh.
    fn stop_mic(&mut self) {
        if !self.mic_talking {
            return;
        }
        if let Some(session) = &mut self.session {
            session.push_mic(&[]);
        }
        self.mic_talking = false;
    }

    fn pump_mic(&mut self) {
        let talking = !self.cfg.muted
            && (!self.cfg.push_to_talk || self.ptt_held)
            && self
                .session
                .as_ref()
                .map(|s| s.state() == crate::session::State::Connected)
                .unwrap_or(false);
        if !talking {
            if let Some(mic) = &mut self.mic {
                while mic.poll_frame().is_some() {}
            }
            self.stop_mic();
            return;
        }
        let Some(mic) = &mut self.mic else {
            self.stop_mic();
            return;
        };
        while let Some(frame) = mic.poll_frame() {
            let mut adjusted = frame;
            for sample in &mut adjusted {
                *sample = (*sample as f32 * self.cfg.mic_volume)
                    .clamp(-32768.0, 32767.0) as i16;
            }
            let packet = match self.codec.as_mut().map(|c| c.encode(&adjusted)) {
                Some(Ok(p)) => p,
                _ => break,
            };
            if let Some(session) = &mut self.session {
                session.push_mic(&packet);
                self.mic_talking = true;
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
            if let crate::session::Event::Voice { sender, data, volume, .. } = e {
                if !self.decoders.contains_key(sender) {
                    match crate::audio::VoiceDecoder::new() {
                        Ok(decoder) => {
                            self.decoders.insert(*sender, decoder);
                        }
                        Err(error) => {
                            eprintln!("[PavoHorizon/SVC] decoder init failed for {sender}: {error}");
                            continue;
                        }
                    }
                }
                let Some(mut pcm) = self
                    .decoders
                    .get_mut(sender)
                    .and_then(|decoder| decoder.decode(Some(data)).ok())
                else {
                    continue;
                };
                crate::audio::mix(&mut pcm, *volume * self.cfg.volume);
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
            if self.cfg.push_to_talk {
                self.ptt_held = pressed;
            }
            return None;
        }
        if !pressed {
            return None;
        }
        if code.eq_ignore_ascii_case(&self.cfg.mute_key) {
            self.set_muted(!self.cfg.muted);
            return Some(if self.cfg.muted { "microphone muted".into() } else { "microphone unmuted".into() });
        }
        if code.eq_ignore_ascii_case(&self.cfg.mute_players_key) {
            self.set_players_muted(!self.cfg.muted_players);
            return Some(if self.cfg.muted_players { "other players muted".into() } else { "other players unmuted".into() });
        }
        if code.eq_ignore_ascii_case(&self.cfg.mute_all_key) {
            let muted = !(self.cfg.muted && self.cfg.muted_players);
            self.set_all_muted(muted);
            return Some(if muted { "microphone and other players muted".into() } else { "microphone and other players unmuted".into() });
        }
        None
    }

    pub fn on_key_binding(
        &mut self,
        key: &str,
        pressed: bool,
        ptt_binding: &str,
        mute_mic_binding: &str,
        mute_players_binding: &str,
        mute_all_binding: &str,
    ) -> Option<String> {
        if key == ptt_binding || configured_key_matches(key, &self.cfg.ptt_key) {
            if self.cfg.push_to_talk {
                self.ptt_held = pressed;
            }
            return None;
        }
        if !pressed {
            return None;
        }
        if key == mute_mic_binding || configured_key_matches(key, &self.cfg.mute_key) {
            self.set_muted(!self.cfg.muted);
            return Some(if self.cfg.muted { "microphone muted".into() } else { "microphone unmuted".into() });
        }
        if key == mute_players_binding || configured_key_matches(key, &self.cfg.mute_players_key) {
            self.set_players_muted(!self.cfg.muted_players);
            return Some(if self.cfg.muted_players { "other players muted".into() } else { "other players unmuted".into() });
        }
        if key == mute_all_binding || configured_key_matches(key, &self.cfg.mute_all_key) {
            let muted = !(self.cfg.muted && self.cfg.muted_players);
            self.set_all_muted(muted);
            return Some(if muted { "microphone and other players muted".into() } else { "microphone and other players unmuted".into() });
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
                ("microphone muted".into(), sends)
            }
            "unmute" => {
                self.set_muted(false);
                ("microphone unmuted".into(), sends)
            }
            "toggle" => {
                self.set_muted(!self.cfg.muted);
                (if self.cfg.muted { "microphone muted" } else { "microphone unmuted" }.into(), sends)
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
            "mode" => {
                match rest.first().copied() {
                    Some("ptt") => self.set_push_to_talk(true),
                    Some("always") => self.set_push_to_talk(false),
                    Some(_) => return ("usage: /svc mode ptt|always".into(), sends),
                    None => return (format!("microphone mode: {}", if self.cfg.push_to_talk { "push-to-talk" } else { "always-on" }), sends),
                }
                (format!("microphone mode: {}", if self.cfg.push_to_talk { "push-to-talk" } else { "always-on" }), sends)
            }
            "ptt" => {
                if let Some(k) = rest.first() {
                    if !valid_key_code(k) {
                        return ("usage: /svc ptt KeyV".into(), sends);
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
                    "voice chat: {}, mic gain: {}%, output: {}%, PTT: {}",
                    if self.session.as_ref().is_some_and(|s| s.state() == crate::session::State::Connected) { "connected" } else { "disconnected" },
                    (self.cfg.mic_volume * 100.0).round() as u32,
                    (self.cfg.volume * 100.0).round() as u32,
                    format!("{} ({})", if self.cfg.push_to_talk { "push-to-talk" } else { "always-on" }, self.cfg.ptt_key)
                ) + if self.cfg.muted { ", muted" } else { ", unmuted" },
                sends,
            ),
            _ => (
                "/svc mute|unmute|toggle|muteplayers|unmuteplayers|muteall|unmuteall|mode ptt|always|ptt <KeyCode>|volume <0-100>|micvolume <0-100>|status; /vcgroup create|join|leave|list".into(),
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
                let Some(id) = rest.first().and_then(|name| {
                    parse_group_id(name).or_else(|| self.group_by_name.get(*name).copied())
                }) else {
                    return "usage: /vcgroup join <group id or name> [password]".into();
                };
                sends.push((
                    proto::SET_GROUP.to_string(),
                    proto::join_group(id, rest.get(1).copied()),
                ));
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
            "groups" => {
                let mut names: Vec<&String> = self.group_by_name.keys().collect();
                names.sort();
                format!("voice groups: {}", names.iter().map(|s| s.as_str()).collect::<Vec<_>>().join(", "))
            }
            _ => "usage: /vcgroup create|join <group id or name> [password]|leave|list|groups".into(),
        }
    }
}

fn parse_group_id(value: &str) -> Option<u128> {
    if let Ok(id) = value.parse::<u128>() {
        return Some(id);
    }
    let compact: String = value.chars().filter(|ch| *ch != '-').collect();
    if compact.len() == 32 && compact.bytes().all(|ch| ch.is_ascii_hexdigit()) {
        return u128::from_str_radix(&compact, 16).ok();
    }
    None
}

fn valid_key_code(key: &str) -> bool {
    (key.starts_with("Key") && key.len() == 4 && key.as_bytes()[3].is_ascii_alphabetic())
        || (key.starts_with("Digit") && key.len() == 6 && key.as_bytes()[5].is_ascii_digit())
}

fn configured_key_matches(native_key: &str, configured: &str) -> bool {
    let suffix = if configured.starts_with("Key") && configured.len() == 4 {
        configured[3..].to_ascii_lowercase()
    } else if configured.starts_with("Digit") && configured.len() == 6 {
        configured[5..].to_string()
    } else {
        return false;
    };
    native_key == format!("key.keyboard.{suffix}")
}

fn voice_socket(voice_host: &str, hint: Option<&str>, port: u16) -> SocketAddr {
    let fallback = hint.unwrap_or("127.0.0.1");
    if voice_host.is_empty() {
        return socket_of(fallback, port);
    }
    if let Ok(port_override) = voice_host.parse::<u16>() {
        return socket_of(fallback, port_override);
    }
    if let Some((host, port_text)) = voice_host.rsplit_once(':') {
        if host.starts_with('[') || !host.contains(':') {
            if let Ok(port_override) = port_text.parse::<u16>() {
                return socket_of(host, port_override);
            }
        }
    }
    socket_of(voice_host, port)
}

fn socket_of(host: &str, port: u16) -> SocketAddr {
    use std::net::ToSocketAddrs;
    let address = if host.starts_with('[') {
        format!("{host}:{port}")
    } else if host.contains(':') {
        format!("[{host}]:{port}")
    } else {
        format!("{host}:{port}")
    };
    address
        .to_socket_addrs()
        .ok()
        .and_then(|mut it| it.next())
        .unwrap_or_else(|| ([127, 0, 0, 1], port).into())
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
        g.on_net("voicechat:secret", &body, None, Instant::now());
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
    fn replaced_player_uuid_does_not_remove_the_new_mapping() {
        let mut g = glue();
        let now = Instant::now();
        let first = proto::PlayerState {
            uuid: 9,
            name: "player".into(),
            disabled: false,
            disconnected: false,
            group: None,
        };
        let second = proto::PlayerState {
            uuid: 10,
            name: "player".into(),
            disabled: false,
            disconnected: false,
            group: None,
        };
        g.see_state(&first, now);
        g.see_state(&second, now);
        assert_eq!(g.uuid_by_name.get("player"), Some(&10));
        assert!(!g.name_by_uuid.contains_key(&9));

        g.on_net(proto::REMOVE_STATE, &9u128.to_be_bytes(), None, now);
        assert_eq!(g.uuid_by_name.get("player"), Some(&10));
        assert_eq!(g.name_by_uuid.get(&10), Some(&"player".to_string()));
    }

    #[test]
    fn ptt_and_mute_toggle() {
        let mut g = glue();
        assert_eq!(g.on_key("KeyV", true), None);
        assert!(g.ptt_held);
        g.on_key("KeyV", false);
        assert!(!g.ptt_held);
        assert_eq!(g.on_key("KeyM", true), Some("microphone muted".into()));
        assert!(g.cfg.muted);
    }

    #[test]
    fn commands_reply_and_send() {
        let mut g = glue();
        let (reply, sends) = g.on_command("svc mute");
        assert_eq!(reply, "microphone muted");
        assert!(g.cfg.muted);
        let (reply, sends) = g.on_command("vcgroup create den");
        assert!(reply.contains("den"));
        assert_eq!(sends.len(), 1);
        assert_eq!(sends[0].0, proto::CREATE_GROUP);
    }

    #[test]
    fn group_id_parser_accepts_decimal_and_uuid_text() {
        assert_eq!(parse_group_id("123456"), Some(123456));
        assert_eq!(parse_group_id("00000000-0000-0000-0000-000000000001"), Some(1));
        assert_eq!(parse_group_id("0000000000000000000000000000000a"), Some(10));
        assert_eq!(parse_group_id("not-a-group"), None);
    }

    #[test]
    fn group_join_resolves_group_names_and_uuid_ids() {
        let mut g = glue();
        let group_id = 123456u128;
        g.group_by_name.insert("lobby".into(), group_id);
        let (reply, sends) = g.on_command("vcgroup join lobby secret");
        assert!(reply.contains(&group_id.to_string()));
        assert_eq!(sends.len(), 1);
        assert_eq!(sends[0].0, proto::SET_GROUP);
        assert_eq!(sends[0].1, proto::join_group(group_id, Some("secret")));

        let (reply, sends) = g.on_command("vcgroup join 123456");
        assert!(reply.contains("123456"));
        assert_eq!(sends[0].1, proto::join_group(group_id, None));
    }

    #[test]
    fn voice_controls_toggle_independently() {
        let mut g = glue();
        g.set_muted(true);
        assert!(g.cfg.muted);
        assert!(!g.cfg.muted_players);
        g.set_players_muted(true);
        assert!(g.cfg.muted);
        assert!(g.cfg.muted_players);
        g.set_muted(false);
        assert!(!g.cfg.muted);
        assert!(g.cfg.muted_players);
        g.set_players_muted(false);
        assert!(!g.cfg.muted_players);
    }

    #[test]
    fn configured_key_codes_work_with_native_key_events() {
        let mut g = glue();
        g.cfg.ptt_key = "KeyX".into();
        g.cfg.mute_key = "KeyY".into();
        assert_eq!(g.on_key_binding("key.keyboard.x", true, "key.keyboard.v", "key.keyboard.m", "key.keyboard.b", "key.keyboard.n"), None);
        assert!(g.ptt_held);
        g.on_key_binding("key.keyboard.x", false, "key.keyboard.v", "key.keyboard.m", "key.keyboard.b", "key.keyboard.n");
        assert!(!g.ptt_held);
        assert_eq!(g.on_key_binding("key.keyboard.y", true, "key.keyboard.v", "key.keyboard.m", "key.keyboard.b", "key.keyboard.n"), Some("microphone muted".into()));
        assert!(g.cfg.muted);
    }

    #[test]
    fn always_on_mode_does_not_require_ptt() {
        let mut g = glue();
        g.set_push_to_talk(false);
        assert!(!g.cfg.push_to_talk);
        assert_eq!(g.on_key("KeyV", true), None);
        assert!(!g.ptt_held);
    }

    #[test]
    fn voice_commands_restore_volume_and_mode_controls() {
        let mut g = glue();
        let (reply, _) = g.on_command("svc volume 45");
        assert_eq!(reply, "voice output volume: 45%");
        let (reply, _) = g.on_command("svc micvolume 30");
        assert_eq!(reply, "microphone gain: 30%");
        assert!((g.cfg.volume - 0.45).abs() < 0.001);
        assert!((g.cfg.mic_volume - 0.30).abs() < 0.001);
        let (reply, _) = g.on_command("svc mode always");
        assert!(reply.contains("always-on"));
        assert!(!g.cfg.push_to_talk);
    }

    #[test]
    fn config_round_trip() {
        let dir = std::env::temp_dir().join(format!("svc-test-{}", std::process::id()));
        let cfg = VoiceConfig { ptt_key: "KeyB".into(), muted: true, volume: 0.5, ..VoiceConfig::default() };
        Glue::save_config(dir.to_str().unwrap(), &cfg);
        let loaded = Glue::load_config(dir.to_str().unwrap());
        assert_eq!(loaded.ptt_key, "KeyB");
        assert!(loaded.muted);
        assert_eq!(loaded.volume, 0.5);
        let _ = std::fs::remove_dir_all(&dir);
    }
}

#[cfg(test)]
mod voice_address_tests {
    use super::*;

    #[test]
    fn empty_voice_host_uses_the_game_server_address() {
        assert_eq!(
            voice_socket("", Some("127.0.0.1"), 24454),
            "127.0.0.1:24454".parse::<SocketAddr>().unwrap()
        );
    }

    #[test]
    fn voice_host_can_override_only_the_udp_port() {
        assert_eq!(
            voice_socket("24455", Some("127.0.0.1"), 24454),
            "127.0.0.1:24455".parse::<SocketAddr>().unwrap()
        );
    }

    #[test]
    fn voice_host_can_override_host_and_port() {
        assert_eq!(
            voice_socket("127.0.0.2:24456", Some("127.0.0.1"), 24454),
            "127.0.0.2:24456".parse::<SocketAddr>().unwrap()
        );
    }
}
