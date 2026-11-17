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
        Some((tx, rx, invalid)) => format!("UDP TX/RX/ERR: {}/{}/{}", tx, rx, invalid),
        None => "UDP: ESPERANDO SECRET".into(),
    };
    let error_line = udp_error
        .map(|error| format!("UDP ERROR: {}", error))
        .unwrap_or_else(|| "UDP ERROR: -".into());
    let lines = vec![
        ("SIMPLE VOICE CHAT [H]".to_string(), 0xFFFFFF),
        (format!("Conexion: {}", if connected { "CONECTADO" } else { "DESCONECTADO" }), if connected { 0x55FF55 } else { 0xFF5555 }),
        (format!("Microfono: {}", if muted { "SILENCIADO" } else if mic { "ACTIVO" } else { "INACTIVO" }), if muted { 0xFF5555 } else if mic { 0x55FF55 } else { 0xFFAA00 }),
        (format!("Salida: {}", if muted_players { "SILENCIADA" } else if speaker { "ACTIVA" } else { "INACTIVA" }), if muted_players { 0xFF5555 } else if speaker { 0x55FF55 } else { 0xFFAA00 }),
        (format!("PTT {}: {}", ptt, if ptt_held { "TRANSMITIENDO" } else { "ESPERANDO" }), if ptt_held { 0x55FF55 } else { 0xFFFFFF }),
        (format!("Volumen: {}% | Ganancia mic: {}%", volume, mic_volume), 0xFFFFFF),
        (udp_line, 0xFFFFFF),
        (error_line, if udp_error.is_none() { 0x55FF55 } else { 0xFF5555 }),
    ];
    let width = lines.iter()
        .map(|(line, _)| game.native_text_width(line))
        .fold(0.0_f32, f32::max) + 10.0;
    let height = lines.len() as f32 * 10.0 + 8.0;
    let (x, y) = corners.place(corner, width, height);
    game.native_fill(batch, [x, y, width, height], 0xB8000000);
    for (index, (line, color)) in lines.iter().enumerate() {
        game.native_text(batch, line, [x + 5.0, y + 4.0 + index as f32 * 10.0], *color, true);
    }
}
