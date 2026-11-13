fn hud(
    game: &mut crate::game::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    corners: &mut horizon_client::extras_hud::Corners,
) {
    let (open, connected, permission, mic, speaker, ptt, ptt_held, muted,
        stage, tx_count, rx_count, last_tx, last_rx, tx_bytes, rx_bytes,
        net_error, history, udp_counts, udp_error) = {
        let guard = crate::game::simple_voice_chat::state()
            .lock()
            .unwrap_or_else(|error| error.into_inner());
        let udp_counts = guard.session.as_ref().map(|session| session.udp_counts());
        let udp_error = guard.session.as_ref().and_then(|session| session.last_udp_error()).map(str::to_string);
        (
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
            guard.cfg.ptt_key.clone(),
            guard.ptt_held,
            guard.cfg.muted,
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

    if !open {
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
