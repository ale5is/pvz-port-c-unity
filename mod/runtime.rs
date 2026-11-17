// Game-side module for the Simple Voice Chat port.
// The shared svc_client crate owns protocol, UDP, audio and persisted settings.

use std::sync::Mutex;
use std::time::Instant;

pub use svc_client::runtime::{state, Glue};

static OUT: Mutex<Vec<(String, Vec<u8>)>> = Mutex::new(Vec::new());

fn config_base() -> Option<String> {
    match crate::game::Game::native_config_dir() {
        Ok(path) => Some(path.to_string_lossy().into_owned()),
        Err(error) => {
            eprintln!("[PavoHorizon/SVC] cannot resolve config directory: {error}");
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

pub fn world_open_ready() {
    if let Some(base) = config_base() {
        locked().cfg = svc_client::runtime::Glue::load_config(&base);
    }
    {
        let mut g = locked();
        g.world_open();
        let (channel, body) = Glue::handshake();
        queue_outbound(channel, body);
        for note in g.start_audio() {
            eprintln!("{note}");
        }
    }
}

pub fn world_closed() {
    locked().world_close();
    OUT.lock().unwrap_or_else(|e| e.into_inner()).clear();
}

fn server_host(address: &str) -> Option<String> {
    let address = address.trim();
    if let Ok(socket) = address.parse::<std::net::SocketAddr>() {
        return Some(socket.ip().to_string());
    }
    if let Some(bracketed) = address.strip_prefix('[')
        && let Some(end) = bracketed.find(']')
    {
        return Some(bracketed[..end].to_string());
    }
    if let Some((host, port)) = address.rsplit_once(':')
        && !host.contains(':')
        && port.parse::<u16>().is_ok()
    {
        return Some(host.to_string());
    }
    Some(address.to_string())
}

pub fn on_net_from_game(
    game: &crate::game::Game<'_>,
    channel: &str,
    data: &[u8],
) {
    let enabled = game.native_on("simple-voice-chat");
    eprintln!("[PavoHorizon/SVC] GAME RX channel={} bytes={} enabled={}", channel, data.len(), enabled);
    if !enabled {
        return;
    }
    let hint = game
        .remote
        .as_ref()
        .and_then(|remote| server_host(&remote.session.address));
    locked().on_net(channel, data, hint.as_deref(), Instant::now());
}

static REMOTE_ACTIVE: std::sync::atomic::AtomicBool =
    std::sync::atomic::AtomicBool::new(false);

pub fn on_frame(game: &mut crate::game::Game<'_>, _elapsed: std::time::Duration) {
    if !game.native_on("simple-voice-chat") {
        if REMOTE_ACTIVE.swap(false, std::sync::atomic::Ordering::Relaxed) {
            world_closed();
        }
        return;
    }

    let connected = game
        .remote
        .as_ref()
        .is_some_and(|remote| remote.logged_in && remote.joined && remote.reconfiguring.is_none());
    let was_connected = REMOTE_ACTIVE.swap(connected, std::sync::atomic::Ordering::Relaxed);

    if connected && !was_connected {
        world_open_ready();
    } else if !connected && was_connected {
        world_closed();
    }

    let (listener_position, player_positions) = game.native_voice_positions();
    let reconnect = {
        let mut g = locked();
        let resolved_positions: Vec<_> = player_positions
            .into_iter()
            .filter_map(|(name, position)| {
                g.uuid_by_name.get(&name).copied().map(|uuid| (uuid, position))
            })
            .collect();
        if let Some(session) = g.session.as_mut() {
            if let Some(position) = listener_position {
                session.set_listener(position);
            }
            session.replace_positions(resolved_positions);
        }
        let _ = g.on_frame(Instant::now());
        std::mem::take(&mut g.reconnect_pending)
    };
    if connected && reconnect {
        let (channel, body) = Glue::handshake();
        queue_outbound(channel, body);
    }
}

pub fn on_key(game: &crate::game::Game<'_>, key: &str, pressed: bool) {
    if !game.native_on("simple-voice-chat") || game.player.is_none() {
        return;
    }
    let ptt = game.native_mod_key("simple-voice-chat", "ptt", "key.keyboard.v");
    let mute_mic = game.native_mod_key("simple-voice-chat", "mute_mic", "key.keyboard.m");
    let mute_players = game.native_mod_key("simple-voice-chat", "mute_players", "key.keyboard.b");
    let mute_all = game.native_mod_key("simple-voice-chat", "mute_all", "key.keyboard.n");
    let feedback = locked().on_key_binding(
        key,
        pressed,
        &ptt,
        &mute_mic,
        &mute_players,
        &mute_all,
    );
    if let Some(feedback) = feedback {
        save_config();
        eprintln!("[PavoHorizon/SVC] {feedback}");
    }
}

pub fn on_command(game: &crate::game::Game<'_>, command: &str, ran: &mut bool) {
    if !game.native_on("simple-voice-chat") {
        return;
    }
    let command = command.trim();
    let command = command.strip_prefix('/').unwrap_or(command);
    let head = command.split_whitespace().next().unwrap_or("");
    if head != "svc" && head != "vcgroup" {
        return;
    }
    let (feedback, outbound) = locked().on_command(command);
    save_config();
    if !feedback.is_empty() {
        eprintln!("[PavoHorizon/SVC] {feedback}");
    }
    for (channel, body) in outbound {
        queue_outbound(channel, body);
    }
    *ran = true;
}

pub fn register_mod(mods: &mut Vec<crate::game::native_hooks::NativeMod>) {
    if mods.iter().any(|m| m.id == "simple-voice-chat") {
        return;
    }
    if let Some(base) = config_base() {
        locked().cfg = svc_client::runtime::Glue::load_config(&base);
    }
    mods.push(crate::game::native_hooks::NativeMod {
        id: "simple-voice-chat".into(),
        title: "Simple Voice Chat".into(),
        description: "Voice chat with nearby players. Configure push-to-talk and independent mute controls.".into(),
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
    buttons.push(format!(
        "Output volume: {}% (-10%)",
        (g.cfg.volume * 100.0).round() as u32
    ));
    buttons.push(format!(
        "Output volume: {}% (+10%)",
        (g.cfg.volume * 100.0).round() as u32
    ));
    buttons.push(format!(
        "Microphone gain: {}% (-10%)",
        (g.cfg.mic_volume * 100.0).round() as u32
    ));
    buttons.push(format!(
        "Microphone gain: {}% (+10%)",
        (g.cfg.mic_volume * 100.0).round() as u32
    ));
}

pub fn settings_click(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    button: usize,
) {
    if mod_id != "simple-voice-chat" {
        return;
    }
    if button <= 3 {
        let (description, cfg) = {
            let mut g = locked();
            match button {
                0 => {
                    let enabled = !g.cfg.push_to_talk;
                    g.set_push_to_talk(enabled);
                    (if enabled { "push-to-talk mode enabled" } else { "always-on microphone enabled" }, g.cfg.clone())
                }
                1 => {
                    let muted = !g.cfg.muted;
                    g.set_muted(muted);
                    (if muted { "microphone muted" } else { "microphone unmuted" }, g.cfg.clone())
                }
                2 => {
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
        eprintln!("[PavoHorizon/SVC] {description}; voice session remains active");
        return;
    }
    if (5..=8).contains(&button) {
        let (description, cfg) = {
            let mut g = locked();
            let (label, value) = match button {
                5 => {
                    g.cfg.volume = (g.cfg.volume - 0.1).clamp(0.0, 1.0);
                    ("output volume", g.cfg.volume)
                }
                6 => {
                    g.cfg.volume = (g.cfg.volume + 0.1).clamp(0.0, 1.0);
                    ("output volume", g.cfg.volume)
                }
                7 => {
                    g.cfg.mic_volume = (g.cfg.mic_volume - 0.1).clamp(0.0, 1.0);
                    ("microphone gain", g.cfg.mic_volume)
                }
                _ => {
                    g.cfg.mic_volume = (g.cfg.mic_volume + 0.1).clamp(0.0, 1.0);
                    ("microphone gain", g.cfg.mic_volume)
                }
            };
            (format!("{label}: {}%", (value * 100.0).round() as u32), g.cfg.clone())
        };
        if let Some(base) = config_base() {
            svc_client::runtime::Glue::save_config(&base, &cfg);
        }
        eprintln!("[PavoHorizon/SVC] {description}");
        return;
    }
    if button != 4 {
        return;
    }
    if !game.native_on("simple-voice-chat") || game.player.is_none() {
        eprintln!("[PavoHorizon/SVC] reconnect requires the mod enabled and a world open");
        return;
    }
    world_closed();
    world_open_ready();
    eprintln!("[PavoHorizon/SVC] manual reconnect requested");
}
