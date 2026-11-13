use horizon_client::{
    extras::Corner,
    extras_hud::Corners,
    hud::HudBatch,
};

fn hud(
    game: &mut crate::game::Game<'_>,
    batch: &mut HudBatch,
    corners: &mut Corners,
) {
    let (open, connected, permission, mic, speaker, ptt, ptt_held, muted) = {
        let guard = crate::game::simple_voice_chat::state()
            .lock()
            .unwrap_or_else(|error| error.into_inner());
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
        )
    };

    if !open {
        return;
    }

    let corner = Corner::TopLeft;
    if !game.native_corner_shown(corner) {
        return;
    }

    let ptt_line = format!(
        "PTT {}: {}",
        ptt,
        if ptt_held { "TRANSMITIENDO" } else { "ESPERANDO" }
    );
    let lines = [
        ("SIMPLE VOICE CHAT  [H]", 0xFFFFFF),
        (
            if connected { "Conexion: CONECTADO" } else { "Conexion: DESCONECTADO" },
            if connected { 0x55FF55 } else { 0xFF5555 },
        ),
        (
            match permission {
                "CONCEDIDO" => "Permiso microfono: CONCEDIDO",
                "DENEGADO" => "Permiso microfono: DENEGADO",
                "ERROR JNI" => "Permiso microfono: ERROR JNI",
                "SIN MUNDO" => "Permiso microfono: SIN MUNDO",
                _ => "Permiso microfono: COMPROBANDO",
            },
            if permission == "CONCEDIDO" { 0x55FF55 } else { 0xFFAA00 },
        ),
        (
            if mic { "Captura: ACTIVA" } else { "Captura: INACTIVA" },
            if mic { 0x55FF55 } else { 0xFFAA00 },
        ),
        (
            if speaker { "Salida de audio: ACTIVA" } else { "Salida de audio: INACTIVA" },
            if speaker { 0x55FF55 } else { 0xFFAA00 },
        ),
        (
            ptt_line.as_str(),
            if ptt_held { 0x55FF55 } else { 0xFFFFFF },
        ),
        (
            if muted { "Microfono: SILENCIADO" } else { "Microfono: HABILITADO" },
            if muted { 0xFF5555 } else { 0x55FF55 },
        ),
    ];

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
