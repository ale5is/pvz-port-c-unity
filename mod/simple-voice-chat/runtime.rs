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
    if !locked().cfg.enabled {
        OUT.lock().unwrap_or_else(|e| e.into_inner()).clear();
        return Vec::new();
    }
    std::mem::take(&mut *OUT.lock().unwrap_or_else(|e| e.into_inner()))
}

pub fn world_open_ready(game: &mut crate::game::Game<'_>) {
    {
        let mut g = locked();
        if let Some(base) = config_base() {
            g.cfg = svc_client::runtime::Glue::load_config(&base);
        }
        // The Mods list is the source of truth. A stale config flag must not
        // override the native mod's Yes/No switch.
        g.cfg.enabled = game.native_on("simple-voice-chat");
        if !g.cfg.enabled {
            return;
        }
    }
    let mut g = locked();
    g.world_open();
    #[cfg(not(target_os = "android"))]
    g.set_audio_permission(true);
    let (channel, body) = Glue::handshake();
    queue_outbound(channel, body);
}

pub fn world_closed() {
    locked().world_close();
    OUT.lock().unwrap_or_else(|e| e.into_inner()).clear();
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
    let mut g = locked();
    if g.cfg.enabled {
        g.on_net(channel, data, server_address, Instant::now());
    }
}

pub fn record_net_tx(channel: &str, bytes: usize) {
    locked().record_tx(channel, bytes, Instant::now());
}

pub fn on_key(game: &crate::game::Game<'_>, key: &str, pressed: bool) {
    if !game.native_on("simple-voice-chat") || game.player.is_none() {
        return;
    }
    let playing = game.native_playing();
    let ptt_binding = game.native_mod_key("simple-voice-chat", "ptt", "key.keyboard.v");
    let mute_binding = game.native_mod_key("simple-voice-chat", "mute", "key.keyboard.m");
    let changed = {
        let mut g = locked();
        if !g.cfg.enabled {
            return;
        }
        if playing && key == "key.keyboard.h" && pressed {
            g.hud_open = !g.hud_open;
        }
        if playing || !pressed {
            g.on_key_binding(key, pressed, &ptt_binding, &mute_binding).is_some()
        } else {
            false
        }
    };
    if changed {
        save_config();
    }
}

pub fn on_command(game: &mut crate::game::Game<'_>, command: &str, ran: &mut bool) {
    if !game.native_on("simple-voice-chat") || !locked().cfg.enabled {
        return;
    }
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
    let enabled = game.native_on("simple-voice-chat");
    locked().cfg.enabled = enabled;
    if !enabled {
        if locked().is_world_active() {
            world_closed();
        } else {
            OUT.lock().unwrap_or_else(|e| e.into_inner()).clear();
        }
        return;
    }
    // A screen such as inventory, chat or options can make native_playing()
    // false while the world and player are still alive. Keep the SVC session
    // for the lifetime of the player; close it only after the player is gone.
    // Remote clients do not receive native world-lifecycle callbacks.
    let world_present = game.player.is_some();
    let active = locked().is_world_active();
    if world_present && !active {
        world_open_ready(game);
    } else if !world_present && active {
        world_closed();
    }

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

pub fn register_mod(
    _game: &mut crate::game::Game<'_>,
    mods: &mut Vec<crate::game::native_hooks::NativeMod>,
) {
    if mods.iter().any(|m| m.id == "simple-voice-chat") {
        return;
    }
    mods.push(crate::game::native_hooks::NativeMod {
        id: "simple-voice-chat".into(),
        title: "Simple Voice Chat".into(),
        description: "Voice chat with nearby players. Configure push-to-talk and mute keys below.".into(),
        keys: vec![
            ["ptt".into(), "Push to Talk".into(), "key.keyboard.v".into()],
            ["mute".into(), "Mute Microphone".into(), "key.keyboard.m".into()],
        ],
    });
}

pub fn settings(
    _game: &mut crate::game::Game<'_>,
    mod_id: &str,
    buttons: &mut Vec<String>,
) {
    if mod_id != "simple-voice-chat" {
        return;
    }
    buttons.push("Reconnect to voice server".into());
}

pub fn settings_click(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    button: usize,
) {
    if mod_id != "simple-voice-chat" || button != 0 {
        return;
    }
    if !game.native_on("simple-voice-chat") || game.player.is_none() {
        eprintln!("[voice] reconnect is available only when Simple Voice Chat is enabled in Mods and a world is open");
        return;
    }
    world_closed();
    world_open_ready(game);
    eprintln!("[voice] manual reconnect requested");
}
