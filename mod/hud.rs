fn hud(
    game: &mut crate::game::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    corners: &mut horizon_client::extras_hud::Corners,
) {
    let (open, connected, mic, speaker, ptt, ptt_held, muted, muted_players, volume, mic_volume, udp_counts, udp_error) = {
        let guard = crate::game::simple_voice_chat::state()
            .lock()
            .unwrap_or_else(|error| error.into_inner());
        let connected = guard.session.as_ref().is_some_and(|session| {
            session.state() == svc_client::session::State::Connected
        });
        let udp_counts = guard.session.as_ref().map(|session| session.udp_counts());
        let udp_error = guard.session.as_ref()
            .and_then(|session| session.last_udp_error())
            .map(str::to_string);
        (
            guard.hud_open,
            connected,
            guard.mic.is_some(),
            guard.speaker.is_some(),
            guard.cfg.ptt_key.clone(),
            guard.ptt_held,
            guard.cfg.muted,
            guard.cfg.muted_players,
            (guard.cfg.volume * 100.0).round() as u32,
            (guard.cfg.mic_volume * 100.0).round() as u32,
            udp_counts,
            udp_error,
        )
    };
    if !open {
        return;
    }
    let corner = horizon_client::extras::Corner::TopLeft;
    if !game.native_corner_shown(corner) {
        return;
    }
    let udp_line = match udp_counts {
        Some((tx, rx, invalid)) if horizon_client::lang::code().starts_with("es") => {
            format!("UDP TX/RX/ERR: {}/{}/{}", tx, rx, invalid)
        }
        Some((tx, rx, invalid)) => format!("UDP TX/RX/ERR: {}/{}/{}", tx, rx, invalid),
        None if horizon_client::lang::code().starts_with("es") => "UDP: ESPERANDO CLAVE".into(),
        None => "UDP: WAITING FOR SECRET".into(),
    };
    let has_udp_error = udp_error.is_some();
    let error_line = udp_error
        .map(|error| {
            if horizon_client::lang::code().starts_with("es") {
                format!("ERROR UDP: {}", error)
            } else {
                format!("UDP ERROR: {}", error)
            }
        })
        .unwrap_or_else(|| {
            if horizon_client::lang::code().starts_with("es") { "ERROR UDP: -".into() } else { "UDP ERROR: -".into() }
        });
    let spanish = horizon_client::lang::code().starts_with("es");
    let hud_key = game.native_mod_key("simple-voice-chat", "hud_toggle", "key.keyboard.h");
    let hud_key_label = hud_key.rsplit('.').next().unwrap_or(&hud_key).to_uppercase();
    let lines = if spanish {
        vec![
            (format!("PAVOHORIZON | VOZ [{}]", hud_key_label), 0xFFFFFF),
            (format!("Conexión: {}", if connected { "CONECTADO" } else { "DESCONECTADO" }), if connected { 0x55FF55 } else { 0xFF5555 }),
            (format!("Micrófono: {}", if muted { "SILENCIADO" } else if mic { "ACTIVO" } else { "INACTIVO" }), if muted { 0xFF5555 } else if mic { 0x55FF55 } else { 0xFFAA00 }),
            (format!("Salida: {}", if muted_players { "SILENCIADA" } else if speaker { "ACTIVA" } else { "INACTIVA" }), if muted_players { 0xFF5555 } else if speaker { 0x55FF55 } else { 0xFFAA00 }),
            (format!("PTT {}: {}", ptt, if ptt_held { "TRANSMITIENDO" } else { "ESPERANDO" }), if ptt_held { 0x55FF55 } else { 0xFFFFFF }),
            (format!("Volumen: {}% | Ganancia mic.: {}%", volume, mic_volume), 0xFFFFFF),
            (udp_line, 0xFFFFFF),
            (error_line, if has_udp_error { 0xFF5555 } else { 0x55FF55 }),
        ]
    } else {
        vec![
            (format!("PAVOHORIZON | VOICE [{}]", hud_key_label), 0xFFFFFF),
            (format!("Connection: {}", if connected { "CONNECTED" } else { "DISCONNECTED" }), if connected { 0x55FF55 } else { 0xFF5555 }),
            (format!("Microphone: {}", if muted { "MUTED" } else if mic { "ACTIVE" } else { "INACTIVE" }), if muted { 0xFF5555 } else if mic { 0x55FF55 } else { 0xFFAA00 }),
            (format!("Output: {}", if muted_players { "MUTED" } else if speaker { "ACTIVE" } else { "INACTIVE" }), if muted_players { 0xFF5555 } else if speaker { 0x55FF55 } else { 0xFFAA00 }),
            (format!("PTT {}: {}", ptt, if ptt_held { "TRANSMITTING" } else { "WAITING" }), if ptt_held { 0x55FF55 } else { 0xFFFFFF }),
            (format!("Volume: {}% | Mic gain: {}%", volume, mic_volume), 0xFFFFFF),
            (if udp_counts.is_some() { format!("UDP TX/RX/ERR: {}", udp_line.trim_start_matches("UDP TX/RX/ERR: ")) } else { "UDP: WAITING FOR SECRET".into() }, 0xFFFFFF),
            (if has_udp_error { format!("UDP ERROR: {}", error_line.trim_start_matches("UDP ERROR: ")) } else { "UDP ERROR: -".into() }, if has_udp_error { 0xFF5555 } else { 0x55FF55 }),
        ]
    };
    let width = lines.iter()
        .map(|(line, _)| game.native_text_width(line))
        .fold(0.0_f32, f32::max) + 10.0;
    let height = lines.len() as f32 * 10.0 + 8.0;
    let (x, y) = corners.place(corner, width, height);
    game.native_fill(batch, [x, y, width, height], 0xB8000000);
    for (index, (line, color)) in lines.iter().enumerate() {
        game.native_text(batch, line, [x + 5.0, y + 4.0 + index as f32 * 10.0], *color, true);
    }

    let (configured_ptt_key, ptt_enabled) = {
        let guard = crate::game::simple_voice_chat::state()
            .lock()
            .unwrap_or_else(|error| error.into_inner());
        (guard.cfg.ptt_key.clone(), guard.cfg.push_to_talk)
    };
    if ptt_enabled {
        let native_ptt_key = game.native_mod_key("simple-voice-chat", "ptt", "key.keyboard.v");
        let ptt_key = if native_ptt_key == "key.keyboard.v" && configured_ptt_key != "KeyV" {
            configured_ptt_key
        } else {
            native_ptt_key.rsplit('.').next().unwrap_or(&native_ptt_key).to_owned()
        };
        let ptt_label = ptt_key.strip_prefix("Key").unwrap_or(&ptt_key).to_uppercase();
        let prompt = if spanish {
            format!("Pulsa [{}] para hablar", ptt_label)
        } else {
            format!("Press [{}] to talk", ptt_label)
        };
        let prompt_width = game.native_text_width(&prompt) + 10.0;
        let (prompt_x, prompt_y) = corners.place(
            horizon_client::extras::Corner::BottomLeft,
            prompt_width,
            16.0,
        );
        game.native_fill(batch, [prompt_x, prompt_y, prompt_width, 16.0], 0xB8000000);
        game.native_text(batch, &prompt, [prompt_x + 5.0, prompt_y + 4.0], 0xFFFFFF, true);
    }
}
