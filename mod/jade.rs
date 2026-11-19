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
    buttons.push(option_label(game, "show_block_states", [
        "Show block states", "Mostrar estados del bloque", "Mostrar estados do bloco", "Afficher l’état du bloc",
        "Mostra stati del blocco", "Blockzustände anzeigen", "ブロック状態を表示", "显示方块状态",
    ]));
    buttons.push(option_label(game, "show_block_info", [
        "Show block info", "Mostrar información del bloque", "Mostrar informações do bloco", "Afficher les infos du bloc",
        "Mostra info blocco", "Blockinfos anzeigen", "ブロック情報を表示", "显示方块信息",
    ]));
    buttons.push(option_label(game, "show_item_mod_name", [
        "Show item mod name", "Mostrar mod del objeto", "Mostrar mod do item", "Afficher le mod de l’objet",
        "Mostra mod oggetto", "Item-Mod anzeigen", "アイテムのMod名を表示", "显示物品模组名称",
    ]));
    buttons.push(option_label(game, "show_mod_name", [
        "Show block mod name", "Mostrar mod del bloque", "Mostrar mod do bloco", "Afficher le mod du bloc",
        "Mostra mod del blocco", "Block-Mod anzeigen", "ブロックのMod名を表示", "显示方块模组名称",
    ]));
    buttons.push(option_label(game, "display_entities", [
        "Display entities", "Mostrar entidades", "Exibir entidades", "Afficher les entités",
        "Mostra entità", "Entitäten anzeigen", "エンティティを表示", "显示实体",
    ]));
    buttons.push(option_label(game, "show_entity_health", [
        "Show entity health", "Mostrar vida de entidades", "Mostrar vida das entidades", "Afficher la santé des entités",
        "Mostra salute entità", "Entitätsgesundheit anzeigen", "エンティティの体力を表示", "显示实体生命值",
    ]));
    buttons.push(option_label(game, "display_dropped_items", [
        "Display dropped items", "Mostrar objetos tirados", "Exibir itens largados", "Afficher les objets au sol",
        "Mostra oggetti a terra", "Abgeworfene Gegenstände anzeigen", "ドロップアイテムを表示", "显示掉落物品",
    ]));
    buttons.push(option_label(game, "show_item_count", [
        "Show item count", "Mostrar cantidad de objetos", "Mostrar quantidade de itens", "Afficher la quantité",
        "Mostra quantità oggetti", "Gegenstandsanzahl anzeigen", "アイテム数を表示", "显示物品数量",
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
        3 => "show_block_states",
        4 => "show_block_info",
        5 => "show_item_mod_name",
        6 => "show_mod_name",
        7 => "display_entities",
        8 => "show_entity_health",
        9 => "display_dropped_items",
        10 => "show_item_count",
        _ => return,
    };
    let next = !enabled(game, key, true);
    game.native_set_setting(ID, key, if next { "true" } else { "false" });
}

pub fn item_tooltip(
    game: &super::Game<'_>,
    stack: &horizon_client::items::ItemStack,
    mut lines: Vec<(horizon_client::text::Text, u32)>,
) -> Vec<(horizon_client::text::Text, u32)> {
    if !game.native_on(ID) || !enabled(game, "show_item_mod_name", true) {
        return lines;
    }
    let Some((namespace, _)) = stack.item.split_once(':') else {
        return lines;
    };
    if namespace == "minecraft" || namespace.is_empty() {
        return lines;
    }
    let label = tr([
        "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "模组",
    ]);
    lines.push((
        horizon_client::text::Text::literal(format!("{label}: {namespace}")),
        horizon_client::hud::rgba(170, 170, 170, 255),
    ));
    lines
}

pub fn hud(game: &mut super::Game<'_>, batch: &mut horizon_client::hud::HudBatch) {
    if !game.native_on(ID) || !enabled(game, "display_tooltip", true) {
        return;
    }

    // Dropped-item hit test: use the same entity reach and ray/AABB test as mobs.
    let item_hit = if enabled(game, "display_dropped_items", true) {
        game.aim().and_then(|(eye, dir)| {
            game.dropped.iter().enumerate()
                .filter_map(|(index, item)| {
                    super::ray_box(eye, dir, game.entity_reach, item.bounds())
                        .map(|distance| (index, distance))
                })
                .min_by(|a, b| a.1.total_cmp(&b.1))
        })
    } else {
        None
    };

    // Jade's entity providers: an entity is shown only when its hit is nearer than
    // the first block outline, so walls continue to occlude the overlay.
    if enabled(game, "display_entities", true) {
        if let (Some((index, distance)), Some((eye, _))) = (game.mob_hit(), game.aim()) {
            let block_distance = game.pick_unfiltered().map(|block| {
                (0..3)
                    .map(|axis| (block.point[axis] - eye[axis]).powi(2))
                    .sum::<f64>()
                    .sqrt()
            });
            if block_distance.is_none_or(|block_distance| distance < block_distance)
                && item_hit.is_none_or(|(_, item_distance)| distance <= item_distance)
            {
                if let Some(mob) = game.mobs.get(index) {
                    let id: &str = mob.kind.id.as_str();
                    let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                    let title = mob.custom_name.clone().unwrap_or_else(|| {
                        game.translate(&format!("entity.minecraft.{short_name}"), short_name)
                    });
                    let mut details = Vec::with_capacity(2);
                    if enabled(game, "show_mod_name", true) {
                        if let Some((namespace, _)) = id.split_once(':') {
                            if namespace != "minecraft" && !namespace.is_empty() {
                                details.push(format!("{}: {}", tr([
                                    "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "模组",
                                ]), namespace));
                            }
                        }
                    }
                    if enabled(game, "show_entity_health", true) {
                        details.push(format!(
                            "{}: {:.1}/{:.1}",
                            tr(["Health", "Vida", "Vida", "Santé", "Salute", "Gesundheit", "体力", "生命值"]),
                            mob.health.max(0.0),
                            mob.attrs.max_health.max(1.0),
                        ));
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
                    for (line_index, detail) in details.iter().enumerate() {
                        batch.text(
                            atlas,
                            detail,
                            panel_x + 6.0,
                            panel_y + 16.0 + line_index as f32 * 11.0,
                            0xFFB8B8B8,
                            true,
                        );
                    }
                    return;
                }
            }
        }
    }

    if let Some((index, distance)) = item_hit {
        let block_distance = game.aim().and_then(|(eye, _)| {
            game.pick_unfiltered().map(|block| {
                (0..3)
                    .map(|axis| (block.point[axis] - eye[axis]).powi(2))
                    .sum::<f64>()
                    .sqrt()
            })
        });
        let mob_distance = game.mob_hit().map(|(_, mob_distance)| mob_distance);
        if block_distance.is_none_or(|block_distance| distance < block_distance)
            && mob_distance.is_none_or(|mob_distance| distance < mob_distance)
        {
            if let Some(item) = game.dropped.get(index) {
                let id = item.stack.item.as_ref();
                let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                let title = game.translate(&format!("item.{id}"), short_name);
                let mut details = Vec::with_capacity(3);
                if enabled(game, "show_mod_name", true) {
                    if let Some((namespace, _)) = id.split_once(':') {
                        if namespace != "minecraft" && !namespace.is_empty() {
                            details.push(format!("{}: {}", tr([
                                "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "模组",
                            ]), namespace));
                        }
                    }
                }
                if enabled(game, "show_registry_id", true) {
                    details.push(id.to_owned());
                }
                if enabled(game, "show_item_count", true) {
                    details.push(format!("{}: {}", tr([
                        "Count", "Cantidad", "Quantidade", "Quantité", "Quantità", "Anzahl", "個数", "数量",
                    ]), item.stack.count));
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
                for (line_index, detail) in details.iter().enumerate() {
                    batch.text(
                        atlas,
                        detail,
                        panel_x + 6.0,
                        panel_y + 16.0 + line_index as f32 * 11.0,
                        0xFFB8B8B8,
                        true,
                    );
                }
                return;
            }
        }
    }

    let Some(hit) = game.pick() else {
        return;
    };

    let state = game.world.block(hit.pos[0], hit.pos[1], hit.pos[2]);
    let Some(block_state) = game.pack.assets.registry.state(state) else {
        return;
    };
    let name = block_state.name.as_str();
    if matches!(name, "minecraft:air" | "minecraft:cave_air" | "minecraft:void_air") {
        return;
    }

    let short_name = name.rsplit(':').next().unwrap_or(name);
    let mut chars = short_name.chars();
    let title = match chars.next() {
        Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
        None => short_name.to_owned(),
    }.replace('_', " ");

    let mut details = Vec::with_capacity(5);
    if enabled(game, "show_mod_name", true) {
        if let Some((namespace, _)) = name.split_once(':') {
            if namespace != "minecraft" && !namespace.is_empty() {
                details.push(format!("{}: {}", tr([
                    "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "模组",
                ]), namespace));
            }
        }
    }
    if enabled(game, "show_registry_id", true) {
        details.push(name.to_owned());
    }
    if enabled(game, "show_coordinates", true) {
        details.push(format!("X: {}  Y: {}  Z: {}", hit.pos[0], hit.pos[1], hit.pos[2]));
    }
    if enabled(game, "show_block_states", true) && !block_state.properties.is_empty() {
        let properties = block_state.properties.iter()
            .map(|(key, value)| format!("{key}={value}"))
            .collect::<Vec<_>>()
            .join(", ");
        details.push(format!("{}: {}", tr([
            "State", "Estado", "Estado", "État", "Stato", "Zustand", "状態", "状态",
        ]), properties));
    }
    if enabled(game, "show_block_info", true) {
        let facts = game.res.facts().get(state);
        let hardness = if facts.hardness < 0.0 {
            tr(["Unbreakable", "Irrompible", "Inquebrável", "Incassable", "Indistruttibile", "Unzerstörbar", "破壊不可", "不可破坏"]).to_owned()
        } else {
            format!("{}: {:.1}", tr(["Hardness", "Dureza", "Dureza", "Dureté", "Durezza", "Härte", "硬度", "硬度"]), facts.hardness)
        };
        let tool = if facts.requires_tool {
            tr(["Tool required", "Requiere herramienta", "Requer ferramenta", "Outil requis", "Richiede strumento", "Werkzeug erforderlich", "道具が必要", "需要工具"])
        } else {
            tr(["No tool required", "No requiere herramienta", "Não requer ferramenta", "Aucun outil requis", "Non richiede strumenti", "Kein Werkzeug nötig", "道具不要", "不需要工具"])
        };
        details.push(format!("{hardness} · {tool}"));
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
