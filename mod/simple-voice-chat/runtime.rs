// Game-side module for the Simple Voice Chat port. Thin shell over the
// svc_client bundle crate: protocol, session and audio live there; this
// module bridges Horizon's hooks to it and queues outbound packets for
// the net send path.

use std::sync::Mutex;
use std::time::Instant;

pub use svc_client::runtime::{state, Glue};

static OUT: Mutex<Vec<(String, Vec<u8>)>> = Mutex::new(Vec::new());

fn locked() -> std::sync::MutexGuard<'static, Glue> {
    state().lock().unwrap_or_else(|e| e.into_inner())
}

pub fn queue_outbound(channel: String, body: Vec<u8>) {
    OUT.lock()
        .unwrap_or_else(|e| e.into_inner())
        .push((channel, body));
}

pub fn drain_outbound() -> Vec<(String, Vec<u8>)> {
    std::mem::take(&mut *OUT.lock().unwrap_or_else(|e| e.into_inner()))
}

pub fn world_open_ready() {
    #[cfg(target_os = "android")]
    if let Err(error) = crate::native_ime::request_audio_permission() {
        eprintln!("[voice] microphone permission request failed: {error}");
    }
    let mut g = locked();
    g.world_open();
    let (channel, body) = Glue::handshake();
    queue_outbound(channel, body);
    for note in g.ensure_audio(Instant::now()) {
        eprintln!("{note}");
    }
}

pub fn world_closed() {
    locked().world_close();
}

pub fn world_lifecycle(
    _game: &mut crate::game::Game<'_>,
    _context: &crate::game::native_lifecycle::NativeLifecycleContext,
    phase: crate::game::native_lifecycle::WorldPhase,
    _error_text: &str,
) {
    use crate::game::native_lifecycle::WorldPhase::{WorldCloseFailed, WorldCloseFinal, WorldOpenFailed, WorldOpenReady};
    match phase {
        WorldOpenReady => world_open_ready(),
        WorldOpenFailed | WorldCloseFinal | WorldCloseFailed => world_closed(),
    }
}

pub fn server_address(game: &crate::game::Game<'_>) -> Option<&str> {
    game.remote
        .as_ref()
        .map(|remote| remote.session.address.as_str())
}

pub fn on_net(channel: &str, data: &[u8], server_address: Option<&str>) {
    locked().on_net(channel, data, server_address, Instant::now());
}

pub fn on_key(key: &str, pressed: bool) {
    let code = key.strip_prefix("key.keyboard.")
        .and_then(|name| {
            let mut chars = name.chars();
            let first = chars.next()?;
            if chars.next().is_none() && first.is_ascii_alphanumeric() {
                Some(format!("Key{}", first.to_ascii_uppercase()))
            } else {
                None
            }
        });
    if let Some(code) = code {
        locked().on_key(&code, pressed);
    }
}

pub fn on_command(command: &str, ran: &mut bool) {
    let command = command.trim();
    if !svc_client::runtime::is_voice_command(command) {
        return;
    }
    let command = command.strip_prefix('/').unwrap_or(command);
    let (feedback, outbound) = locked().on_command(command);
    if !feedback.is_empty() {
        eprintln!("{feedback}");
    }
    for (channel, body) in outbound {
        queue_outbound(channel, body);
    }
    *ran = true;
}

pub fn on_frame(game: &mut crate::game::Game<'_>, _elapsed: std::time::Duration) {
    let (listener, players) = game.native_voice_positions();
    let now = Instant::now();
    let mut g = locked();
    if let (Some(listener), Some(session)) = (listener, g.session.as_mut()) {
        session.set_positions(listener, players);
    }
    for note in g.ensure_audio(now) {
        eprintln!("{note}");
    }
    let _ = g.on_frame(now);
}
