//! Experimental Horizon Nightly adapter boundary for Simple Voice Chat.
//!
//! This crate deliberately isolates the new mod API from the legacy mixin adapter.
//! It registers vanilla-compatible raw plugin channels and Controls bindings, and
//! exposes bounded queues for the existing SVC runtime to consume. It does not yet
//! start UDP/audio or claim that the complete voice mod is operational.

use mod_api::{
    self, ClientHost, ClientPacketEvent, ClientTickPostEvent, Hooks, KeyConflictContext,
    KeyRegistration, PayloadId, Registrar, RegistrationContext,
};
use std::collections::VecDeque;
use std::sync::{
    Mutex, OnceLock,
    atomic::{AtomicBool, Ordering},
};

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
}

static INCOMING: OnceLock<Mutex<VecDeque<IncomingPayload>>> = OnceLock::new();
static CONTROLS: OnceLock<Mutex<VecDeque<Control>>> = OnceLock::new();
static LAST_PTT: AtomicBool = AtomicBool::new(false);

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

#[derive(Default)]
struct SimpleVoiceChat;

#[mod_api::horizon_mod]
impl Hooks for SimpleVoiceChat {
    fn register(&self, registrar: &mut dyn Registrar, _: &RegistrationContext) {
        let mut payload_event = registrar.payload_handlers();
        let mut payloads = payload_event.registrar("1");
        for channel in CHANNELS {
            payloads.play_raw(PayloadId((*channel).into()), None);
        }

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

    fn client_tick_post(&self, host: &mut dyn ClientHost, _: &mut ClientTickPostEvent) {
        let ptt = host.is_down(MOD_ID, "ptt");
        if LAST_PTT.swap(ptt, Ordering::Relaxed) != ptt {
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
