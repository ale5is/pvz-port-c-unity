// API-80 adaptation of Jade's in-world look-at overlay for Horizon 0.7.8.
// This module deliberately uses only the pinned game's existing APIs.

const ID: &str = "jade";

fn enabled(game: &super::Game<'_>, key: &str, default: bool) -> bool {
    game.native_setting(ID, key)
        .and_then(|value| match value.as_str() {
            "true" => Some(true),
            "false" => Some(false),
            _ => None,
        })
        .unwrap_or(default)
}

fn language_index() -> usize {
    let code = horizon_client::lang::code().to_ascii_lowercase();
    if code.starts_with("es") { 1 }
    else if code.starts_with("pt") { 2 }
    else if code.starts_with("fr") { 3 }
    else if code.starts_with("it") { 4 }
    else if code.starts_with("de") { 5 }
    else if code.starts_with("ja") { 6 }
    else if code.starts_with("zh") { 7 }
    else { 0 }
}

fn tr(values: [&'static str; 8]) -> &'static str {
    values[language_index()]
}

fn option_label(game: &super::Game<'_>, key: &str, label: [&'static str; 8]) -> String {
    let value = if enabled(game, key, true) {
        tr(["ON", "ACTIVADO", "ATIVADO", "ACTIVÉ", "ATTIVO", "EIN", "オン", "开启"])
    } else {
        tr(["OFF", "DESACTIVADO", "DESATIVADO", "DÉSACTIVÉ", "DISATTIVO", "AUS", "オフ", "关闭"])
    };
    format!("{}: {}", tr(label), value)
}

pub fn register_mod(mods: &mut Vec<crate::game::native_hooks::NativeMod>) {
    if mods.iter().any(|m| m.id == ID) {
        return;
    }
    mods.push(crate::game::native_hooks::NativeMod {
        id: ID.into(),
        title: "Jade".into(),
        description: tr([
            "Shows information about the block you are looking at.",
            "Muestra información del bloque que estás mirando.",
            "Mostra informações sobre o bloco que você está olhando.",
            "Affiche des informations sur le bloc que vous regardez.",
            "Mostra informazioni sul blocco che stai guardando.",
            "Zeigt Informationen über den Block an, den du ansiehst.",
            "見ているブロックの情報を表示します。",
            "显示你正在注视的方块信息。",
        ]).into(),
        keys: Vec::new(),
    });
}

pub fn settings(
    game: &mut super::Game<'_>,
    mod_id: &str,
    buttons: &mut Vec<String>,
) {
    if mod_id != ID {
        return;
    }
    buttons.push(option_label(game, "display_tooltip", [
        "Display tooltip", "Mostrar información", "Exibir informações", "Afficher l’infobulle",
        "Mostra suggerimento", "Tooltip anzeigen", "ツールチップを表示", "显示提示框",
    ]));
    buttons.push(option_label(game, "show_registry_id", [
        "Show registry ID", "Mostrar ID del registro", "Mostrar ID do registro", "Afficher l’identifiant",
        "Mostra ID registro", "Registrierungs-ID anzeigen", "登録 ID を表示", "显示注册 ID",
    ]));
    buttons.push(option_label(game, "show_coordinates", [
        "Show coordinates", "Mostrar coordenadas", "Mostrar coordenadas", "Afficher les coordonnées",
        "Mostra coordinate", "Koordinaten anzeigen", "座標を表示", "显示坐标",
    ]));
}

pub fn settings_click(game: &mut super::Game<'_>, mod_id: &str, button: usize) {
    if mod_id != ID {
        return;
    }
    let key = match button {
        0 => "display_tooltip",
        1 => "show_registry_id",
        2 => "show_coordinates",
        _ => return,
    };
    let next = !enabled(game, key, true);
    game.native_set_setting(ID, key, if next { "true" } else { "false" });
}

pub fn hud(game: &mut super::Game<'_>, batch: &mut horizon_client::hud::HudBatch) {
    if !game.native_on(ID) || !enabled(game, "display_tooltip", true) {
        return;
    }

    let Some(hit) = game.pick() else {
        return;
    };

    let state = game.world.block(hit.pos[0], hit.pos[1], hit.pos[2]);
    let name = &game.res.facts().get(state).name;
    let name = &**name;
    if matches!(name, "minecraft:air" | "minecraft:cave_air" | "minecraft:void_air") {
        return;
    }

    let short_name = name.rsplit(':').next().unwrap_or(name);
    let mut chars = short_name.chars();
    let title = match chars.next() {
        Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
        None => short_name.to_owned(),
    }.replace('_', " ");

    let mut details = Vec::with_capacity(2);
    if enabled(game, "show_registry_id", true) {
        details.push(name.to_owned());
    }
    if enabled(game, "show_coordinates", true) {
        details.push(format!("X: {}  Y: {}  Z: {}", hit.pos[0], hit.pos[1], hit.pos[2]));
    }

    let atlas = &game.gui.atlas;
    let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
    let detail_width = details.iter()
        .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
        .fold(0.0_f32, f32::max);
    let panel_width = (title_width.max(detail_width) + 12.0)
        .min((batch.width - 16.0).max(0.0));
    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
    let panel_y = 8.0;
    let panel_height = 17.0 + details.len() as f32 * 12.0;

    batch.fill(atlas, panel_x, panel_y, panel_width, panel_height, 0xD0101010);
    batch.text(atlas, &title, panel_x + 6.0, panel_y + 4.0, 0xFFFFFFFF, true);
    for (index, detail) in details.iter().enumerate() {
        batch.text(
            atlas,
            detail,
            panel_x + 6.0,
            panel_y + 16.0 + index as f32 * 11.0,
            0xFFB8B8B8,
            true,
        );
    }
}
