// Game-side module for the Simple Voice Chat port. Thin shell over the
// svc_client bundle crate: protocol, session and audio live there; this
// module bridges Horizon's hooks to it and queues outbound packets for
// the net send path.

use std::sync::Mutex;
use std::time::Instant;

pub use svc_client::runtime::{state, Glue};

static OUT: Mutex<Vec<(String, Vec<u8>)>> = Mutex::new(Vec::new());
static LAST_POSITION_UPDATE: Mutex<Option<Instant>> = Mutex::new(None);

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

fn voice_active() -> bool {
    let g = locked();
    g.cfg.enabled && g.cfg.voice_enabled
}

fn should_update_voice_positions(now: Instant, connected: bool) -> bool {
    let mut last = LAST_POSITION_UPDATE
        .lock()
        .unwrap_or_else(|error| error.into_inner());
    if !connected {
        *last = None;
        return false;
    }
    if last.map_or(true, |previous| now.duration_since(previous) >= std::time::Duration::from_millis(50)) {
        *last = Some(now);
        true
    } else {
        false
    }
}


pub fn hud_audio_settings() -> (String, bool) {
    let guard = locked();
    (guard.cfg.ptt_key.clone(), guard.cfg.muted)
}

pub fn queue_outbound(channel: String, body: Vec<u8>) {
    OUT.lock()
        .unwrap_or_else(|e| e.into_inner())
        .push((channel, body));
}

pub fn drain_outbound() -> Vec<(String, Vec<u8>)> {
    if !voice_active() {
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
        if !g.cfg.enabled || !g.cfg.voice_enabled || game.player.is_none() {
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
    *LAST_POSITION_UPDATE.lock().unwrap_or_else(|error| error.into_inner()) = None;
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
    if g.cfg.enabled && g.cfg.voice_enabled {
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
    let mute_mic_binding = game.native_mod_key("simple-voice-chat", "mute_mic", "key.keyboard.m");
    let mute_players_binding = game.native_mod_key("simple-voice-chat", "mute_players", "key.keyboard.b");
    let mute_all_binding = game.native_mod_key("simple-voice-chat", "mute_all", "key.keyboard.n");
    let changed = {
        let mut g = locked();
        if !g.cfg.enabled || !g.cfg.voice_enabled {
            return;
        }
        if playing && key == "key.keyboard.h" && pressed {
            g.hud_open = !g.hud_open;
        }
        if playing || !pressed {
            g.on_key_binding(key, pressed, &ptt_binding, &mute_mic_binding, &mute_players_binding, &mute_all_binding).is_some()
        } else {
            false
        }
    };
    if changed {
        save_config();
    }
}

pub fn on_command(game: &mut crate::game::Game<'_>, command: &str, ran: &mut bool) {
    if !game.native_on("simple-voice-chat") || !voice_active() {
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
    let (active, voice_enabled) = {
        let mut g = locked();
        g.cfg.enabled = enabled;
        (g.is_world_active(), g.cfg.voice_enabled)
    };
    if !enabled || !voice_enabled {
        if active {
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
    if world_present && !active {
        world_open_ready(game);
    } else if !world_present && active {
        world_closed();
    }

    let now = Instant::now();
    let connected = locked()
        .session
        .as_ref()
        .is_some_and(|session| session.state() == svc_client::session::State::Connected);
    if should_update_voice_positions(now, connected) {
        let (listener, players) = game.native_voice_positions();
        if let Some(listener) = listener {
            if let Some(session) = locked().session.as_mut() {
                session.set_positions(listener, players);
            }
        }
    }
    let mut g = locked();
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
    if let Some(base) = config_base() {
        locked().cfg = svc_client::runtime::Glue::load_config(&base);
    }
    mods.push(crate::game::native_hooks::NativeMod {
        id: "simple-voice-chat".into(),
        title: "Simple Voice Chat".into(),
        description: "Voice chat with nearby players. Configure push-to-talk and mute keys below.".into(),
        keys: vec![
            ["ptt".into(), "Push to Talk".into(), "key.keyboard.v".into()],
            ["mute_mic".into(), "Mute My Microphone".into(), "key.keyboard.m".into()],
            ["mute_players".into(), "Mute Other Players".into(), "key.keyboard.b".into()],
            ["mute_all".into(), "Mute Microphone + Players".into(), "key.keyboard.n".into()],
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
    let g = locked();
    buttons.push(if g.cfg.voice_enabled {
        "Desactivar chat de voz".into()
    } else {
        "Activar chat de voz".into()
    });
    buttons.push(if g.cfg.push_to_talk {
        "Microphone mode: Push to Talk".into()
    } else {
        "Microphone mode: Always On".into()
    });
    buttons.push(if g.cfg.muted {
        "Unmute my microphone".into()
    } else {
        "Mute my microphone".into()
    });
    buttons.push(if g.cfg.muted_players {
        "Unmute other players".into()
    } else {
        "Mute other players".into()
    });
    buttons.push(if g.cfg.muted && g.cfg.muted_players {
        "Unmute microphone + players".into()
    } else {
        "Mute microphone + players".into()
    });
    buttons.push("Reconnect to voice server".into());
}

pub fn settings_click(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    button: usize,
) {
    if mod_id != "simple-voice-chat" {
        return;
    }
    if button == 0 {
        let (enabled, cfg) = {
            let mut g = locked();
            g.cfg.voice_enabled = !g.cfg.voice_enabled;
            (g.cfg.voice_enabled, g.cfg.clone())
        };
        if let Some(base) = config_base() {
            svc_client::runtime::Glue::save_config(&base, &cfg);
        }
        if enabled {
            world_open_ready(game);
            game.system_message(horizon_client::text::Text::literal("Chat de voz activado"));
        } else {
            world_closed();
            game.system_message(horizon_client::text::Text::literal("Chat de voz desactivado; el chat de texto sigue disponible"));
        }
        return;
    }
    if (1..=4).contains(&button) {
        let (description, cfg) = {
            let mut g = locked();
            match button {
                1 => {
                    let enabled = !g.cfg.push_to_talk;
                    g.set_push_to_talk(enabled);
                    (if enabled { "push-to-talk mode enabled" } else { "always-on microphone enabled" }, g.cfg.clone())
                }
                2 => {
                    let muted = !g.cfg.muted;
                    g.set_muted(muted);
                    (if muted { "microphone muted" } else { "microphone unmuted" }, g.cfg.clone())
                }
                3 => {
                    let muted = !g.cfg.muted_players;
                    g.set_players_muted(muted);
                    (if muted { "other players muted" } else { "other players unmuted" }, g.cfg.clone())
                }
                _ => {
                    let muted = !(g.cfg.muted && g.cfg.muted_players);
                    g.set_all_muted(muted);
                    (if muted { "microphone and other players muted" } else { "microphone and other players unmuted" }, g.cfg.clone())
                }
            }
        };
        if let Some(base) = config_base() {
            svc_client::runtime::Glue::save_config(&base, &cfg);
        }
        eprintln!("[voice] {description}; connection remains active");
        return;
    }
    if button != 5 {
        return;
    }
    if !game.native_on("simple-voice-chat") || game.player.is_none() || !voice_active() {
        eprintln!("[voice] reconnect is available only when Simple Voice Chat is enabled and voice chat is active in a world");
        return;
    }
    world_closed();
    world_open_ready(game);
    eprintln!("[voice] manual reconnect requested");
}

pub fn on_hud(
    game: &mut crate::game::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    corners: &mut horizon_client::extras_hud::Corners,
) {
    let (ptt, muted) = crate::game::simple_voice_chat::hud_audio_settings();
    let (voice_enabled, open, connected, permission, mic, speaker, ptt_held,
        stage, tx_count, rx_count, last_tx, last_rx, tx_bytes, rx_bytes,
        net_error, history, udp_counts, udp_error) = {
        let guard = crate::game::simple_voice_chat::state()
            .lock()
            .unwrap_or_else(|error| error.into_inner());
        let udp_counts = guard.session.as_ref().map(|session| session.udp_counts());
        let udp_error = guard.session.as_ref().and_then(|session| session.last_udp_error()).map(str::to_string);
        (
            guard.cfg.voice_enabled,
            guard.hud_open,
            guard.session.as_ref().is_some_and(|session| {
                session.state() == svc_client::session::State::Connected
            }),
            if !guard.is_world_active() {
                "SIN MUNDO"
            } else if !guard.permission_checked {
                "COMPROBANDO"
            } else if guard.permission_check_error {
                "ERROR JNI"
            } else if guard.audio_permission_granted {
                "CONCEDIDO"
            } else {
                "DENEGADO"
            },
            guard.mic.is_some(),
            guard.speaker.is_some(),
            guard.ptt_held,
            guard.connection_stage.clone(),
            guard.net_tx_count,
            guard.net_rx_count,
            guard.last_tx_channel.clone(),
            guard.last_rx_channel.clone(),
            guard.last_tx_bytes,
            guard.last_rx_bytes,
            guard.last_net_error.clone(),
            guard.net_history.clone(),
            udp_counts,
            udp_error,
        )
    };

    if !voice_enabled || !open {
        return;
    }

    let corner = horizon_client::extras::Corner::TopLeft;
    if !game.native_corner_shown(corner) {
        return;
    }

    let ptt_line = format!(
        "PTT {}: {}",
        ptt,
        if ptt_held { "TRANSMITIENDO" } else { "ESPERANDO" }
    );
    let udp_line = match udp_counts {
        Some((tx, rx, invalid)) => format!("UDP TX/RX/ERR: {}/{}/{}", tx, rx, invalid),
        None => "UDP: ESPERANDO SECRET".into(),
    };
    let tx_line = format!("MC TX #{}: {} ({}B)", tx_count, last_tx, tx_bytes);
    let rx_line = format!("MC RX #{}: {} ({}B)", rx_count, last_rx, rx_bytes);
    let error_line = if !net_error.is_empty() {
        format!("NET ERROR: {}", net_error)
    } else if let Some(error) = udp_error.as_ref() {
        format!("UDP ERROR: {}", error)
    } else {
        "ERROR: -".into()
    };
    let mut lines: Vec<(String, u32)> = vec![
        ("SIMPLE VOICE CHAT [H]".into(), 0xFFFFFF),
        (format!("Conexion: {}", if connected { "CONECTADO" } else { "DESCONECTADO" }), if connected { 0x55FF55 } else { 0xFF5555 }),
        (format!("Etapa: {}", stage), if connected { 0x55FF55 } else { 0xFFAA00 }),
        (format!("Permiso microfono: {}", permission), if permission == "CONCEDIDO" { 0x55FF55 } else { 0xFFAA00 }),
        (format!("Captura: {}", if mic { "ACTIVA" } else { "INACTIVA" }), if mic { 0x55FF55 } else { 0xFFAA00 }),
        (format!("Salida audio: {}", if speaker { "ACTIVA" } else { "INACTIVA" }), if speaker { 0x55FF55 } else { 0xFFAA00 }),
        (ptt_line, if ptt_held { 0x55FF55 } else { 0xFFFFFF }),
        (format!("Microfono: {}", if muted { "SILENCIADO" } else { "HABILITADO" }), if muted { 0xFF5555 } else { 0x55FF55 }),
        (tx_line, 0xFFFFFF),
        (rx_line, 0xFFFFFF),
        (udp_line, 0xFFFFFF),
        (error_line, if net_error.is_empty() && udp_error.is_none() { 0x55FF55 } else { 0xFF5555 }),
    ];
    lines.extend(
        history
            .iter()
            .rev()
            .take(4)
            .rev()
            .map(|event| (format!("NET: {}", event), 0xAAAAAA)),
    );

    let width = lines
        .iter()
        .map(|(line, _)| game.native_text_width(line))
        .fold(0.0_f32, f32::max)
        + 10.0;
    let height = lines.len() as f32 * 10.0 + 8.0;
    let (x, y) = corners.place(corner, width, height);
    game.native_fill(batch, [x, y, width, height], 0xB8000000);

    for (index, (line, color)) in lines.iter().enumerate() {
        game.native_text(
            batch,
            line,
            [x + 5.0, y + 4.0 + index as f32 * 10.0],
            *color,
            true,
        );
    }
}
