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
    let code = horizon_client::lang::code().to_ascii_lowercase();
    let lang = if code.starts_with("es") { 1 } else if code.starts_with("pt") { 2 }
        else if code.starts_with("fr") { 3 } else if code.starts_with("it") { 4 }
        else if code.starts_with("de") { 5 } else if code.starts_with("ja") { 6 }
        else if code.starts_with("zh") { 7 } else { 0 };
    let words = [
        ["VOICE", "Connection", "CONNECTED", "DISCONNECTED", "Microphone", "MUTED", "ACTIVE", "INACTIVE", "Output", "PTT", "TRANSMITTING", "WAITING", "Volume", "Mic gain", "WAITING FOR SECRET", "UDP ERROR", "Press", "to talk"],
        ["VOZ", "Conexión", "CONECTADO", "DESCONECTADO", "Micrófono", "SILENCIADO", "ACTIVO", "INACTIVO", "Salida", "PTT", "TRANSMITIENDO", "ESPERANDO", "Volumen", "Ganancia mic.", "ESPERANDO CLAVE", "ERROR UDP", "Pulsa", "para hablar"],
        ["VOZ", "Conexão", "CONECTADO", "DESCONECTADO", "Microfone", "SILENCIADO", "ATIVO", "INATIVO", "Saída", "PTT", "TRANSMITINDO", "AGUARDANDO", "Volume", "Ganho mic.", "AGUARDANDO CHAVE", "ERRO UDP", "Pressione", "para falar"],
        ["VOIX", "Connexion", "CONNECTÉ", "DÉCONNECTÉ", "Micro", "MUET", "ACTIF", "INACTIF", "Sortie", "PTT", "TRANSMISSION", "EN ATTENTE", "Volume", "Gain micro", "EN ATTENTE DE CLÉ", "ERREUR UDP", "Appuyez", "pour parler"],
        ["VOCE", "Connessione", "CONNESSO", "DISCONNESSO", "Microfono", "MUTED", "ATTIVO", "INATTIVO", "Uscita", "PTT", "TRASMISSIONE", "IN ATTESA", "Volume", "Gain microfono", "IN ATTESA DELLA CHIAVE", "ERRORE UDP", "Premi", "per parlare"],
        ["SPRACHE", "Verbindung", "VERBUNDEN", "GETRENNT", "Mikrofon", "STUMM", "AKTIV", "INAKTIV", "Ausgabe", "PTT", "ÜBERTRÄGT", "WARTET", "Lautstärke", "Mikrofonpegel", "WARTET AUF SCHLÜSSEL", "UDP-FEHLER", "Drücke", "zum Sprechen"],
        ["音声", "接続", "接続中", "切断", "マイク", "ミュート", "有効", "無効", "出力", "PTT", "送信中", "待機中", "音量", "マイク音量", "キー待機中", "UDPエラー", "押して", "話す"],
        ["语音", "连接", "已连接", "已断开", "麦克风", "已静音", "启用", "停用", "输出", "PTT", "正在传输", "等待中", "音量", "麦克风增益", "等待密钥", "UDP错误", "按下", "讲话"]
    ][lang];
    let udp_line = match udp_counts {
        Some((tx, rx, invalid)) => format!("UDP TX/RX/ERR: {}/{}/{}", tx, rx, invalid),
        None => format!("UDP: {}", words[14]),
    };
    let has_udp_error = udp_error.is_some();
    let error_line = udp_error
        .map(|error| format!("{}: {}", words[15], error))
        .unwrap_or_else(|| format!("{}: -", words[15]));
    let hud_key = game.native_mod_key("simple-voice-chat", "hud_toggle", "key.keyboard.h");
    let hud_key_label = hud_key.rsplit('.').next().unwrap_or(&hud_key).to_uppercase();
    let lines = vec![
        (format!("PAVOHORIZON | {} [{}]", words[0], hud_key_label), 0xFFFFFF),
        (format!("{}: {}", words[1], if connected { words[2] } else { words[3] }), if connected { 0x55FF55 } else { 0xFF5555 }),
        (format!("{}: {}", words[4], if muted { words[5] } else if mic { words[6] } else { words[7] }), if muted { 0xFF5555 } else if mic { 0x55FF55 } else { 0xFFAA00 }),
        (format!("{}: {}", words[8], if muted_players { words[5] } else if speaker { words[6] } else { words[7] }), if muted_players { 0xFF5555 } else if speaker { 0x55FF55 } else { 0xFFAA00 }),
        (format!("{} {}: {}", words[9], ptt, if ptt_held { words[10] } else { words[11] }), if ptt_held { 0x55FF55 } else { 0xFFFFFF }),
        (format!("{}: {}% | {}: {}%", words[12], volume, words[13], mic_volume), 0xFFFFFF),
        (udp_line, 0xFFFFFF),
        (error_line, if has_udp_error { 0xFF5555 } else { 0x55FF55 }),
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
        let prompt = format!("{} [{}] {}", words[16], ptt_label, words[17]);
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

    crate::game::atlas_mod::bridge::hud_api80(game, batch, corners);
