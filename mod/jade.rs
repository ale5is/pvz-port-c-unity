// Render item models through Horizon so stack components (notably leather dye) are respected.
fn icon_item_id(id: &str, entity: bool) -> String {
    if entity {
        let (namespace, path) = id.split_once(':').unwrap_or(("minecraft", id));
        format!("{namespace}:{path}_spawn_egg")
    } else {
        id.to_owned()
    }
}

fn icon_available(
    game: &super::Game<'_>,
    atlas: &horizon_client::hud::HudAtlas,
    id: &str,
    entity: bool,
    stack: Option<&horizon_client::items::ItemStack>,
) -> bool {
    if let Some(stack) = stack {
        return game.res.items().get(&stack.item).is_some();
    }
    let item = icon_item_id(id, entity);
    if game.res.items().get(&item).is_some() {
        return true;
    }
    if entity {
        return false;
    }
    let (namespace, path) = id.split_once(':').unwrap_or(("minecraft", id));
    let prefix = if namespace == "minecraft" { String::new() } else { format!("{namespace}:") };
    atlas.sprites.contains_key(&format!("{prefix}block/{path}"))
}

fn draw_icon(
    game: &super::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    atlas: &horizon_client::hud::HudAtlas,
    id: &str,
    entity: bool,
    stack: Option<&horizon_client::items::ItemStack>,
    x: f32,
    y: f32,
) -> bool {
    if let Some(stack) = stack {
        if game.native_item_stack(batch, stack, [x, y], 16.0) {
            return true;
        }
    }
    let item = icon_item_id(id, entity);
    if game.native_item(batch, &item, [x, y], 16.0) {
        return true;
    }
    if entity {
        return false;
    }
    let (namespace, path) = id.split_once(':').unwrap_or(("minecraft", id));
    let prefix = if namespace == "minecraft" { String::new() } else { format!("{namespace}:") };
    let sprite = format!("{prefix}block/{path}");
    if atlas.sprites.contains_key(&sprite) {
        batch.sprite_scaled(atlas, &sprite, x, y, 16.0, 16.0, u32::MAX);
        return true;
    }
    false
}

fn stack_name(
    game: &super::Game<'_>,
    stack: &horizon_client::items::ItemStack,
    fallback: &str,
) -> String {
    game.stack_tooltip(stack)
        .first()
        .map_or_else(|| fallback.to_owned(), |(text, _)| text.plain())
}

const TOOL_TYPES: [&[&str]; 6] = [
    &["minecraft:wooden_pickaxe", "minecraft:golden_pickaxe", "minecraft:stone_pickaxe", "minecraft:copper_pickaxe", "minecraft:iron_pickaxe", "minecraft:diamond_pickaxe", "minecraft:netherite_pickaxe"],
    &["minecraft:wooden_axe", "minecraft:golden_axe", "minecraft:stone_axe", "minecraft:copper_axe", "minecraft:iron_axe", "minecraft:diamond_axe", "minecraft:netherite_axe"],
    &["minecraft:wooden_shovel", "minecraft:golden_shovel", "minecraft:stone_shovel", "minecraft:copper_shovel", "minecraft:iron_shovel", "minecraft:diamond_shovel", "minecraft:netherite_shovel"],
    &["minecraft:wooden_hoe", "minecraft:golden_hoe", "minecraft:stone_hoe", "minecraft:copper_hoe", "minecraft:iron_hoe", "minecraft:diamond_hoe", "minecraft:netherite_hoe"],
    &["minecraft:wooden_sword"],
    &["minecraft:shears"],
];

fn tier_correct(items: &horizon_client::items::Items, tool: &str, block: &str) -> bool {
    let Some(info) = items.get(tool) else { return false };
    for rule in &info.tool {
        if let Some(correct) = rule.correct
            && rule.blocks.iter().any(|entry| {
                entry.strip_prefix('#').map_or(entry == block, |tag| items.block_in_tag(tag, block))
            })
        {
            return correct;
        }
    }
    items.mining_speed(
        Some(&horizon_client::items::ItemStack::new(tool, 1)),
        block,
    ) > info.tool_default_speed
}

fn shear_block(game: &super::Game<'_>, block: &str) -> bool {
    if block == "minecraft:tripwire" {
        return true;
    }
    let Some((namespace, path)) = block.split_once(':') else { return false };
    game.native_data(&format!("data/{namespace}/loot_table/blocks/{path}.json"))
        .is_some_and(|bytes| {
            let text = String::from_utf8_lossy(&bytes);
            text.contains("minecraft:match_tool") && text.contains("minecraft:shears")
        })
}

fn harvest_tools(game: &super::Game<'_>, block: &str) -> Vec<String> {
    let items = game.res.items();
    TOOL_TYPES.iter().filter_map(|tiers| {
        tiers.iter().find(|tool| {
            tier_correct(items, tool, block)
                || (**tool == "minecraft:shears" && shear_block(game, block))
        }).map(|tool| (*tool).to_owned())
    }).collect()
}

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

fn option_label_with_default(
    game: &super::Game<'_>,
    key: &str,
    label: [&'static str; 8],
    default: bool,
) -> String {
    let value = if enabled(game, key, default) {
        tr(["ON", "ACTIVADO", "ATIVADO", "ACTIVÉ", "ATTIVO", "EIN", "オン", "开启"])
    } else {
        tr(["OFF", "DESACTIVADO", "DESATIVADO", "DÉSACTIVÉ", "DISATTIVO", "AUS", "オフ", "关闭"])
    };
    format!("{}: {}", tr(label), value)
}

fn option_label(game: &super::Game<'_>, key: &str, label: [&'static str; 8]) -> String {
    option_label_with_default(game, key, label, true)
}

fn save_enabled(game: &super::Game<'_>, key: &str, value: bool) {
    use std::io::Write;
    let dir = game.native_data_dir(ID);
    let result = (|| -> std::io::Result<()> {
        std::fs::create_dir_all(&dir)?;
        let mut file = std::fs::File::create(dir.join(key))?;
        file.write_all(if value { b"true" } else { b"false" })?;
        file.sync_all()
    })();
    if let Err(error) = result {
        log::warn!("Jade: could not persist setting {key}: {error}");
    } else if game.native_setting(ID, key).as_deref() != Some(if value { "true" } else { "false" }) {
        log::warn!("Jade: setting {key} did not verify after writing");
    }
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
    buttons.push(option_label(game, "show_target_icon", [
        "Show target icon", "Mostrar icono del objetivo", "Mostrar ícone do alvo", "Afficher l’icône ciblée",
        "Mostra icona bersaglio", "Ziel-Symbol anzeigen", "対象アイコンを表示", "显示目标图标",
    ]));
    buttons.push(option_label(game, "show_registry_id", [
        "Show registry ID", "Mostrar ID del registro", "Mostrar ID do registro", "Afficher l’identifiant",
        "Mostra ID registro", "Registrierungs-ID anzeigen", "登録 ID を表示", "显示注册 ID",
    ]));
    buttons.push(option_label_with_default(game, "show_coordinates", [
        "Show coordinates", "Mostrar coordenadas", "Mostrar coordenadas", "Afficher les coordonnées",
        "Mostra coordinate", "Koordinaten anzeigen", "座標を表示", "显示坐标",
    ], false));
    buttons.push(option_label_with_default(game, "show_block_states", [
        "Show block states", "Mostrar estados del bloque", "Mostrar estados do bloco", "Afficher l’état du bloc",
        "Mostra stati del blocco", "Blockzustände anzeigen", "ブロック状態を表示", "显示方块状态",
    ], false));
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
    buttons.push(option_label(game, "display_armor_stands", [
        "Display armor stands", "Mostrar soportes para armaduras", "Exibir suportes de armadura", "Afficher les porte-armures",
        "Mostra supporti per armature", "Rüstungsständer anzeigen", "防具立てを表示", "显示盔甲架",
    ]));
    buttons.push(option_label(game, "show_armor_stand_equipment", [
        "Show armor stand equipment", "Mostrar equipo del soporte", "Mostrar equipamento do suporte", "Afficher l’équipement du porte-armure",
        "Mostra equipaggiamento supporto", "Ausrüstung des Ständers anzeigen", "防具立ての装備を表示", "显示盔甲架装备",
    ]));
}

pub fn settings_click(game: &mut super::Game<'_>, mod_id: &str, button: usize) {
    if mod_id != ID {
        return;
    }
    let key = match button {
        0 => "display_tooltip",
        1 => "show_target_icon",
        2 => "show_registry_id",
        3 => "show_coordinates",
        4 => "show_block_states",
        5 => "show_block_info",
        6 => "show_item_mod_name",
        7 => "show_mod_name",
        8 => "display_entities",
        9 => "show_entity_health",
        10 => "display_dropped_items",
        11 => "show_item_count",
        12 => "display_armor_stands",
        13 => "show_armor_stand_equipment",
        _ => return,
    };
    let default = matches!(button, 3 | 4);
    let next = !enabled(game, key, default);
    save_enabled(game, key, next);
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

    let armor_stand_hit = if enabled(game, "display_armor_stands", true) {
        game.aim().and_then(|(eye, dir)| {
            game.armor_stands.iter().enumerate()
                .filter(|(_, stand)| !stand.marker && !stand.invisible)
                .filter_map(|(index, stand)| {
                    super::ray_box(eye, dir, game.entity_reach, stand.bounds())
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
                && armor_stand_hit.is_none_or(|(_, stand_distance)| distance <= stand_distance)
            {
                if let Some(mob) = game.mobs.get(index) {
                    let id: &str = mob.kind.id.as_str();
                    let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                    let title = mob.custom_name.clone().unwrap_or_else(|| {
                        let (namespace, path) = id.split_once(':').unwrap_or(("minecraft", id));
                        game.translate(&format!("entity.{namespace}.{path}"), path)
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
                    let has_icon = enabled(game, "show_target_icon", true)
                        && icon_sprite(atlas, id, true).is_some();
                    let text_x = if has_icon { 26.0 } else { 6.0 };
                    let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
                    let detail_width = details.iter()
                        .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
                        .fold(0.0_f32, f32::max);
                    let panel_width = (title_width.max(detail_width) + if has_icon { 32.0 } else { 12.0 })
                        .min((batch.width - 16.0).max(0.0));
                    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
                    let panel_y = 8.0;
                    let panel_height = 17.0 + details.len() as f32 * 12.0;
                    batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), 0xD0101010);
                    if has_icon {
                        let _ = draw_icon(batch, atlas, id, true, panel_x + 6.0, panel_y + 2.0);
                    }
                    batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, 0xFFFFFFFF, true);
                    for (line_index, detail) in details.iter().enumerate() {
                        batch.text(
                            atlas,
                            detail,
                            panel_x + text_x,
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
            && armor_stand_hit.is_none_or(|(_, stand_distance)| distance < stand_distance)
        {
            if let Some(item) = game.dropped.get(index) {
                let id = item.stack.item.as_ref();
                let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                let title = stack_name(game, &item.stack, short_name);
                let mut details = Vec::with_capacity(3);
                if enabled(game, "show_item_mod_name", true) {
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
                let has_icon = enabled(game, "show_target_icon", true)
                    && icon_sprite(atlas, id, false).is_some();
                let text_x = if has_icon { 26.0 } else { 6.0 };
                let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
                let detail_width = details.iter()
                    .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
                    .fold(0.0_f32, f32::max);
                let panel_width = (title_width.max(detail_width) + if has_icon { 32.0 } else { 12.0 })
                    .min((batch.width - 16.0).max(0.0));
                let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
                let panel_y = 8.0;
                let panel_height = 17.0 + details.len() as f32 * 12.0;
                batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), 0xD0101010);
                if has_icon {
                    let _ = draw_icon(batch, atlas, id, false, panel_x + 6.0, panel_y + 2.0);
                }
                batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, 0xFFFFFFFF, true);
                for (line_index, detail) in details.iter().enumerate() {
                    batch.text(
                        atlas,
                        detail,
                        panel_x + text_x,
                        panel_y + 16.0 + line_index as f32 * 11.0,
                        0xFFB8B8B8,
                        true,
                    );
                }
                return;
            }
        }
    }

    if let Some((index, distance)) = armor_stand_hit {
        let block_distance = game.aim().and_then(|(eye, _)| {
            game.pick_unfiltered().map(|block| {
                (0..3)
                    .map(|axis| (block.point[axis] - eye[axis]).powi(2))
                    .sum::<f64>()
                    .sqrt()
            })
        });
        let mob_distance = game.mob_hit().map(|(_, mob_distance)| mob_distance);
        let item_distance = item_hit.map(|(_, item_distance)| item_distance);
        if block_distance.is_none_or(|block_distance| distance < block_distance)
            && mob_distance.is_none_or(|mob_distance| distance < mob_distance)
            && item_distance.is_none_or(|item_distance| distance < item_distance)
        {
            if let Some(stand) = game.armor_stands.get(index) {
                let title = game.translate("entity.minecraft.armor_stand", "Armor Stand");
                let mut details = Vec::new();
                if enabled(game, "show_armor_stand_equipment", true) {
                    let equipment = stand.equipment.iter().flatten().map(|stack| {
                        let id = stack.item.as_ref();
                        let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                        let name = stack_name(game, stack, short_name);
                        if stack.count > 1 {
                            format!("{name} x{}", stack.count)
                        } else {
                            name
                        }
                    }).collect::<Vec<_>>();
                    if !equipment.is_empty() {
                        details.push(format!("{}: {}", tr([
                            "Equipment", "Equipo", "Equipamento", "Équipement", "Equipaggiamento", "Ausrüstung", "装備", "装备",
                        ]), equipment.join(", ")));
                    }
                }
                let atlas = &game.gui.atlas;
                let has_icon = enabled(game, "show_target_icon", true)
                    && icon_sprite(atlas, "minecraft:armor_stand", true).is_some();
                let text_x = if has_icon { 26.0 } else { 6.0 };
                let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
                let detail_width = details.iter()
                    .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
                    .fold(0.0_f32, f32::max);
                let panel_width = (title_width.max(detail_width) + if has_icon { 32.0 } else { 12.0 })
                    .min((batch.width - 16.0).max(0.0));
                let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
                let panel_y = 8.0;
                let panel_height = 17.0 + details.len() as f32 * 12.0;
                batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), 0xD0101010);
                if has_icon {
                    let _ = draw_icon(batch, atlas, "minecraft:armor_stand", true, panel_x + 6.0, panel_y + 2.0);
                }
                batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, 0xFFFFFFFF, true);
                for (line_index, detail) in details.iter().enumerate() {
                    batch.text(
                        atlas,
                        detail,
                        panel_x + text_x,
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
    let block_stack = game.picked_stack(hit.pos, false);
    let fallback_title = {
        let mut chars = short_name.chars();
        match chars.next() {
            Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
            None => short_name.to_owned(),
        }.replace('_', " ")
    };
    let title = block_stack.as_ref().map(|stack| stack_name(game, stack, &fallback_title))
        .unwrap_or_else(|| {
            let (namespace, path) = name.split_once(':').unwrap_or(("minecraft", name));
            game.translate(&format!("block.{namespace}.{path}"), &fallback_title)
        });

    let mut details = Vec::with_capacity(5);
    let mut required_tools = Vec::new();
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
            required_tools = harvest_tools(game, name);
            tr(["Tool required", "Requiere herramienta", "Requer ferramenta", "Outil requis", "Richiede strumento", "Werkzeug erforderlich", "道具が必要", "需要工具"])
        } else {
            tr(["No tool required", "No requiere herramienta", "Não requer ferramenta", "Aucun outil requis", "Non richiede strumenti", "Kein Werkzeug nötig", "道具不要", "不需要工具"])
        };
        details.push(format!("{hardness} · {tool}"));
    }
    let atlas = &game.gui.atlas;
    let has_icon = enabled(game, "show_target_icon", true)
        && icon_available(game, atlas, name, false, block_stack.as_ref());
    let text_x = if has_icon { 26.0 } else { 6.0 };
    let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
    let detail_width = details.iter()
        .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
        .fold(0.0_f32, f32::max);
    let tool_label = tr(["Tool", "Herramienta", "Ferramenta", "Outil", "Strumento", "Werkzeug", "道具", "工具"]);
    let has_tool_row = facts.requires_tool && !required_tools.is_empty();
    let tool_row_width = if has_tool_row {
        horizon_client::hud::HudBatch::text_width(atlas, tool_label)
            + required_tools.len() as f32 * 10.0 + 16.0
    } else {
        0.0
    };
    let panel_width = (title_width.max(detail_width).max(tool_row_width) + if has_icon { 32.0 } else { 12.0 })
        .min((batch.width - 16.0).max(0.0));
    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
    let panel_y = 8.0;
    let panel_height = 17.0 + details.len() as f32 * 12.0 + if has_tool_row { 11.0 } else { 0.0 };

    batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), 0xD0101010);
    if has_icon {
        let _ = draw_icon(game, batch, atlas, name, false, block_stack.as_ref(), panel_x + 6.0, panel_y + 2.0);
    }
    batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, 0xFFFFFFFF, true);
    for (index, detail) in details.iter().enumerate() {
        batch.text(
            atlas,
            detail,
            panel_x + text_x,
            panel_y + 16.0 + index as f32 * 11.0,
            0xFFB8B8B8,
            true,
        );
    }
    if has_tool_row {
        let row_y = panel_y + 16.0 + details.len() as f32 * 11.0;
        batch.text(atlas, tool_label, panel_x + text_x, row_y, 0xFFB8B8B8, true);
        let mut icon_x = panel_x + text_x + horizon_client::hud::HudBatch::text_width(atlas, tool_label) + 3.0;
        for tool in &required_tools {
            let _ = game.native_item(batch, tool, [icon_x, row_y], 8.0);
            icon_x += 10.0;
        }
        let correct = game.res.items().correct_for_drops(game.held(), name);
        batch.text(
            atlas,
            if correct { "✔" } else { "✕" },
            icon_x + 1.0,
            row_y,
            if correct { 0xFF55FF55 } else { 0xFFFF5555 },
            true,
        );
    }
}
