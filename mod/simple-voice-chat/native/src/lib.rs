//! Experimental Horizon Nightly adapter for the Simple Voice Chat client.
//!
//! The adapter uses the current native mod API. The protocol, Opus codec, UDP
//! session and audio devices remain in the shared svc_client crate.

use mod_api::{
    self, ClientHost, ClientPacketEvent, ClientTickPostEvent, Command, CommandEvent,
    ConfigPath, Flow, GuiRenderPostEvent, Hooks, KeyConflictContext, KeyRegistration,
    PayloadBytes, PayloadId, Registrar, RegistrationContext, RegisterClientCommandsEvent,
};
use std::collections::VecDeque;
use std::sync::{Mutex, OnceLock};
use std::time::Instant;
use svc_client::runtime::{self, Glue, VoiceConfig};

const MOD_ID: &str = "simple_voice_chat";
const MAX_PENDING_PACKETS: usize = 256;
const MAX_PENDING_CONTROLS: usize = 64;

const CHANNELS: &[&str] = &[
    "voicechat:request_secret",
    "voicechat:secret",
    "voicechat:update_state",
    "voicechat:state",
    "voicechat:states",
    "voicechat:remove_state",
    "voicechat:create_group",
    "voicechat:set_group",
    "voicechat:leave_group",
    "voicechat:joined_group",
    "voicechat:add_group",
    "voicechat:remove_group",
];

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct IncomingPayload {
    pub channel: String,
    pub data: Vec<u8>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Control {
    PushToTalk(bool),
    ToggleMicrophone,
    TogglePlayers,
    ToggleAll,
    ToggleHud,
}

static INCOMING: OnceLock<Mutex<VecDeque<IncomingPayload>>> = OnceLock::new();
static CONTROLS: OnceLock<Mutex<VecDeque<Control>>> = OnceLock::new();

fn incoming() -> &'static Mutex<VecDeque<IncomingPayload>> {
    INCOMING.get_or_init(|| Mutex::new(VecDeque::new()))
}

fn controls() -> &'static Mutex<VecDeque<Control>> {
    CONTROLS.get_or_init(|| Mutex::new(VecDeque::new()))
}

fn push_control(control: Control) {
    let mut queue = controls().lock().unwrap_or_else(|e| e.into_inner());
    if queue.len() == MAX_PENDING_CONTROLS {
        queue.pop_front();
    }
    queue.push_back(control);
}

/// Drain server payloads observed on the Minecraft play connection.
pub fn take_incoming() -> Vec<IncomingPayload> {
    incoming()
        .lock()
        .unwrap_or_else(|e| e.into_inner())
        .drain(..)
        .collect()
}

/// Drain key state changes and button presses for the SVC runtime.
pub fn take_controls() -> Vec<Control> {
    controls()
        .lock()
        .unwrap_or_else(|e| e.into_inner())
        .drain(..)
        .collect()
}

fn config_path() -> Option<ConfigPath> {
    ConfigPath::new("config.json").ok()
}

fn load_config(host: &dyn ClientHost) -> VoiceConfig {
    let Some(path) = config_path() else {
        return VoiceConfig::default();
    };
    host.data_files()
        .read(&path)
        .ok()
        .and_then(|bytes| serde_json::from_slice(&bytes).ok())
        .unwrap_or_default()
}

fn save_config(host: &dyn ClientHost, config: &VoiceConfig) {
    let (Some(path), Ok(bytes)) = (
        config_path(),
        serde_json::to_vec_pretty(config),
    ) else {
        return;
    };
    let _ = host.data_files().write(&path, &bytes);
}

fn apply_control(glue: &mut Glue, control: Control) -> bool {
    const BINDINGS: (&str, &str, &str, &str) =
        ("ptt", "mute_mic", "mute_players", "mute_all");

    match control {
        Control::PushToTalk(held) => {
            let _ = glue.on_key_binding("ptt", held, BINDINGS.0, BINDINGS.1, BINDINGS.2, BINDINGS.3);
            false
        }
        Control::ToggleMicrophone => {
            let note = glue.on_key_binding("mute_mic", true, BINDINGS.0, BINDINGS.1, BINDINGS.2, BINDINGS.3);
            if let Some(note) = note {
                glue.notes.push(format!("[voice] {note}"));
            }
            true
        }
        Control::TogglePlayers => {
            let note = glue.on_key_binding("mute_players", true, BINDINGS.0, BINDINGS.1, BINDINGS.2, BINDINGS.3);
            if let Some(note) = note {
                glue.notes.push(format!("[voice] {note}"));
            }
            true
        }
        Control::ToggleAll => {
            let note = glue.on_key_binding("mute_all", true, BINDINGS.0, BINDINGS.1, BINDINGS.2, BINDINGS.3);
            if let Some(note) = note {
                glue.notes.push(format!("[voice] {note}"));
            }
            true
        }
        Control::ToggleHud => {
            glue.hud_open = !glue.hud_open;
            glue.notes.push(if glue.hud_open {
                "[voice] HUD enabled".into()
            } else {
                "[voice] HUD hidden".into()
            });
            false
        }
    }
}

fn update_player_positions(host: &dyn ClientHost, glue: &mut Glue) {
    let dimension = host.dimension();
    let Some(level) = host.level(&dimension) else {
        return;
    };
    let Some(listener) = host
        .local_player()
        .and_then(|player| host.entity_position(player.entity()))
    else {
        return;
    };

    let min = listener.map(|axis| axis - 256.0);
    let max = listener.map(|axis| axis + 256.0);
    let entities = host.level_entities_in(level, min, max).unwrap_or_default();
    let positions = entities
        .into_iter()
        .filter_map(|entity| {
            let name = host.entity_name(entity)?;
            let uuid = *glue.uuid_by_name.get(&name)?;
            let position = host.entity_position(entity)?;
            Some((uuid, position))
        })
        .collect::<Vec<_>>();

    if let Some(session) = glue.session.as_mut() {
        session.set_positions(listener, positions);
    }
}

fn send_pending(host: &mut dyn ClientHost, glue: &mut Glue, now: Instant) {
    let pending = std::mem::take(&mut glue.pending_sends);
    for (channel, bytes) in pending {
        match host.send_payload_to_server(
            &PayloadId(channel.clone()),
            PayloadBytes(bytes.clone()),
        ) {
            Ok(_) => glue.record_tx(&channel, bytes.len(), now),
            Err(error) => {
                glue.last_net_error = format!("no se pudo enviar {channel}: {error:?}");
                glue.pending_sends.push((channel, bytes));
                break;
            }
        }
    }
}

#[derive(Default)]
struct SimpleVoiceChat;

#[mod_api::horizon_mod]
impl Hooks for SimpleVoiceChat {
    fn register(&self, registrar: &mut dyn Registrar, _: &RegistrationContext) {
        registrar.register_key_mapping(
            KeyRegistration::new("ptt", "key.keyboard.v")
                .label("key.simple_voice_chat.ptt")
                .context(KeyConflictContext::InGame),
        );
        registrar.register_key_mapping(
            KeyRegistration::new("mute_mic", "key.keyboard.m")
                .label("key.simple_voice_chat.mute_mic")
                .context(KeyConflictContext::InGame),
        );
        registrar.register_key_mapping(
            KeyRegistration::new("mute_players", "key.keyboard.b")
                .label("key.simple_voice_chat.mute_players")
                .context(KeyConflictContext::InGame),
        );
        registrar.register_key_mapping(
            KeyRegistration::new("mute_all", "key.keyboard.n")
                .label("key.simple_voice_chat.mute_all")
                .context(KeyConflictContext::InGame),
        );
        registrar.register_key_mapping(
            KeyRegistration::new("hud", "key.keyboard.h")
                .label("key.simple_voice_chat.hud")
                .context(KeyConflictContext::InGame),
        );

        {
            let mut payload_event = registrar.payload_handlers();
            let mut payloads = payload_event.registrar("1");
            for channel in CHANNELS {
                payloads.play_raw(PayloadId((*channel).into()), None);
            }
        }
    }

    fn register_client_commands(
        &self,
        _: &mut dyn ClientHost,
        event: &mut RegisterClientCommandsEvent<'_>,
    ) {
        event.commands.push(Command::new("svc"));
        event.commands.push(Command::new("vcgroup"));
    }

    fn client_packet(&self, _: &mut dyn ClientHost, event: &mut ClientPacketEvent<'_>) {
        let Some(payload) = &event.packet.custom_payload else {
            return;
        };
        if !payload.channel.starts_with("voicechat:") {
            return;
        }

        let mut queue = incoming().lock().unwrap_or_else(|e| e.into_inner());
        if queue.len() == MAX_PENDING_PACKETS {
            queue.pop_front();
        }
        queue.push_back(IncomingPayload {
            channel: payload.channel.clone(),
            data: payload.data.clone(),
        });
    }

    fn command(&self, host: &mut dyn ClientHost, event: &mut CommandEvent<'_>) -> Flow {
        if !runtime::is_voice_command(&event.command) {
            return Flow::Continue;
        }

        let mut glue = runtime::state().lock().unwrap_or_else(|e| e.into_inner());
        let (reply, sends) = glue.on_command(&event.command);
        if !reply.is_empty() {
            glue.notes.push(format!("[voice] {reply}"));
        }
        glue.pending_sends.extend(sends);
        save_config(host, &glue.cfg);
        Flow::Cancel
    }

    fn client_tick_post(&self, host: &mut dyn ClientHost, _: &mut ClientTickPostEvent) {
        let now = Instant::now();
        let server = host.client_session().client_server();
        let mut glue = runtime::state().lock().unwrap_or_else(|e| e.into_inner());

        let Some(server) = server else {
            if glue.is_world_active() {
                glue.world_close();
            }
            let _ = take_incoming();
            let _ = take_controls();
            return;
        };

        if !glue.is_world_active() {
            glue.world_open();
            glue.cfg = load_config(host);
            glue.set_audio_permission(true);
            let (channel, bytes) = Glue::handshake();
            glue.pending_sends.push((channel, bytes));
        }

        let hint = server.address.as_deref();
        for payload in take_incoming() {
            glue.on_net(&payload.channel, &payload.data, hint, now);
        }

        let ptt = host.is_down(MOD_ID, "ptt");
        if ptt != glue.ptt_held {
            push_control(Control::PushToTalk(ptt));
        }
        if host.consume_click(MOD_ID, "mute_mic") {
            push_control(Control::ToggleMicrophone);
        }
        if host.consume_click(MOD_ID, "mute_players") {
            push_control(Control::TogglePlayers);
        }
        if host.consume_click(MOD_ID, "mute_all") {
            push_control(Control::ToggleAll);
        }
        if host.consume_click(MOD_ID, "hud") {
            push_control(Control::ToggleHud);
        }

        let mut config_changed = false;
        for control in take_controls() {
            config_changed |= apply_control(&mut glue, control);
        }

        update_player_positions(host, &mut glue);
        let _ = glue.on_frame(now);
        let audio_notes = glue.ensure_audio(now);
        glue.notes.extend(audio_notes);

        if glue.reconnect_pending {
            if let Some(mut session) = glue.session.take() {
                session.close();
            }
            glue.reconnect_pending = false;
            let (channel, bytes) = Glue::handshake();
            glue.pending_sends.push((channel, bytes));
        }

        if config_changed {
            save_config(host, &glue.cfg);
        }
        send_pending(host, &mut glue, now);
    }

    fn gui_render_post(&self, _: &dyn ClientHost, event: &mut GuiRenderPostEvent<'_>) {
        let glue = runtime::state().lock().unwrap_or_else(|e| e.into_inner());
        if !glue.hud_open {
            return;
        }

        let [width, _] = event.batch.size();
        let panel_width = width.min(300.0).max(120.0);
        event.batch.fill([8.0, 8.0, panel_width, 62.0], 0xb0000000);
        event.batch.text(
            &format!("Simple Voice Chat: {}", glue.connection_stage),
            [16.0, 14.0],
            0xffffffff,
            true,
        );
        for (index, note) in glue.notes.iter().rev().take(2).enumerate() {
            let line = note.chars().take(64).collect::<String>();
            event.batch.text(
                &line,
                [16.0, 34.0 + index as f32 * 13.0],
                0xffdddddd,
                true,
            );
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn channel_catalog_contains_the_complete_svc_control_plane() {
        assert_eq!(CHANNELS.len(), 12);
        assert!(CHANNELS.contains(&"voicechat:request_secret"));
        assert!(CHANNELS.contains(&"voicechat:secret"));
        assert!(CHANNELS.contains(&"voicechat:states"));
        assert!(CHANNELS.contains(&"voicechat:joined_group"));
    }

    #[test]
    fn queues_are_bounded_and_drainable() {
        for i in 0..(MAX_PENDING_CONTROLS + 3) {
            push_control(Control::PushToTalk(i % 2 == 0));
        }
        let drained = take_controls();
        assert_eq!(drained.len(), MAX_PENDING_CONTROLS);
        assert!(take_controls().is_empty());
    }
}
