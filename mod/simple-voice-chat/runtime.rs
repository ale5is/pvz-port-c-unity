// Game-side module for the Simple Voice Chat port. Thin shell over the
// svc_client bundle crate: protocol, session and audio live there; this
// module bridges Horizon's hooks to it and queues outbound packets for
// the net send path.

use std::sync::Mutex;
use std::time::Instant;

pub use svc_client::runtime::{state, Glue};

static OUT: Mutex<Vec<(String, Vec<u8>)>> = Mutex::new(Vec::new());

fn config_base() -> Option<String> {
    match crate::game::Game::native_config_dir() {
        Ok(path) => Some(path.to_string_lossy().into_owned()),
        Err(error) => {
            eprintln!("[voice] cannot resolve config directory: {error}");
            None
        }
    }
}

fn save_config() {
    let Some(base) = config_base() else {
        return;
    };
    let cfg = locked().cfg.clone();
    svc_client::runtime::Glue::save_config(&base, &cfg);
}
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

pub fn world_open_ready(_game: &mut crate::game::Game<'_>) {
    #[cfg(target_os = "android")]
    if let Err(error) = crate::native_ime::request_audio_permission() {
        eprintln!("[voice] microphone permission request failed: {error}");
        locked().set_audio_permission_error();
    }
    let mut g = locked();
    if let Some(base) = config_base() {
        g.cfg = svc_client::runtime::Glue::load_config(&base);
    }
    g.world_open();
    #[cfg(not(target_os = "android"))]
    g.set_audio_permission(true);
    let (channel, body) = Glue::handshake();
    queue_outbound(channel, body);
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
        WorldOpenReady => world_open_ready(_game),
        WorldOpenFailed | WorldCloseFinal | WorldCloseFailed => world_closed(),
    }
}

pub fn server_address<'a, 'w>(game: &'a crate::game::Game<'w>) -> Option<&'a str> {
    game.remote
        .as_ref()
        .map(|remote| remote.session.address.as_str())
}

pub fn on_net(channel: &str, data: &[u8], server_address: Option<&str>) {
    locked().on_net(channel, data, server_address, Instant::now());
}

pub fn on_key(game: &crate::game::Game<'_>, key: &str, pressed: bool) {
    if !game.native_playing() {
        return;
    }
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
        let changed = {
            let mut g = locked();
            if code == "KeyH" && pressed {
                g.hud_open = !g.hud_open;
            }
            g.on_key(&code, pressed).is_some()
        };
        if changed {
            save_config();
        }
    }
}

pub fn on_command(game: &mut crate::game::Game<'_>, command: &str, ran: &mut bool) {
    let command = command.trim();
    if !svc_client::runtime::is_voice_command(command) {
        return;
    }
    let command = command.strip_prefix('/').unwrap_or(command);
    let (feedback, outbound, cfg) = {
        let mut g = locked();
        let (feedback, outbound) = g.on_command(command);
        (feedback, outbound, g.cfg.clone())
    };
    if let Some(base) = config_base() {
        svc_client::runtime::Glue::save_config(&base, &cfg);
    }
    if !feedback.is_empty() {
        game.system_message(horizon_client::text::Text::literal(feedback));
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
    #[cfg(target_os = "android")]
    if g.is_world_active() && g.should_check_audio_permission(now) {
        match crate::native_ime::has_audio_permission() {
            Ok(granted) => g.set_audio_permission(granted),
            Err(error) => {
                g.set_audio_permission_error();
                eprintln!("[voice] microphone permission check failed: {error}");
            },
        }
    }
    #[cfg(not(target_os = "android"))]
    g.set_audio_permission(true);
    for note in g.ensure_audio(now) {
        eprintln!("{note}");
    }
    let _ = g.on_frame(now);
}
