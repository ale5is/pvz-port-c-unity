const ID: &str = "jade";

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
        if game.native_item(batch, &stack.item, [x, y], 16.0) {
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

fn block_entity_details(game: &super::Game<'_>, pos: [i32; 3]) -> Vec<String> {
    use horizon_client::containers::BlockEntity;
    let Some(entity) = game.world.block_entities.get(&(pos[0], pos[1], pos[2])) else {
        return Vec::new();
    };
    let mut out = Vec::new();
    let count_items = |items: &[Option<horizon_client::items::ItemStack>]| items.iter().flatten().count();
    let item_names = |items: &[Option<horizon_client::items::ItemStack>]| {
        items.iter().flatten().take(4).map(|stack| {
            let id = stack.item.as_ref();
            let short = id.split_once(':').map_or(id, |(_, path)| path);
            stack_name(game, stack, short)
        }).collect::<Vec<_>>().join(", ")
    };
    match entity {
        BlockEntity::Chest(items) => {
            out.push(format!("{}: {}/{}", tr(["Stored slots", "Espacios ocupados", "Slots ocupados", "Emplacements utilisés", "Slot occupati", "Belegte Plätze", "使用中スロット", "已占用槽位"]), count_items(items), items.len()));
            let names = item_names(items);
            if !names.is_empty() { out.push(names); }
        }
        BlockEntity::Furnace(furnace) => {
            if furnace.lit_remaining > 0 { out.push(format!("{}: {} s", tr(["Burn time", "Combustible", "Combustível", "Combustion", "Combustione", "Brennzeit", "燃焼時間", "燃烧时间"]), furnace.lit_remaining / 20)); }
            if furnace.cooking_total > 0 { out.push(format!("{}: {}%", tr(["Smelting", "Fundición", "Fundição", "Cuisson", "Fusione", "Schmelzen", "精錬", "冶炼"]), (furnace.cooking_timer * 100 / furnace.cooking_total).clamp(0, 100))); }
            let names = item_names(&furnace.items);
            if !names.is_empty() { out.push(names); }
        }
        BlockEntity::Brewing(brewing) => {
            out.push(format!("{}: {}/3", tr(["Bottles", "Frascos", "Frascos", "Fioles", "Bottiglie", "Flaschen", "瓶", "药瓶"]), count_items(&brewing.items[..3])));
            if brewing.brew_time > 0 { out.push(format!("{}: {} s", tr(["Brewing", "Preparando", "Preparando", "Préparation", "Preparazione", "Brauen", "醸造", "酿造"]), brewing.brew_time / 20)); }
            if brewing.fuel > 0 { out.push(format!("{}: {}", tr(["Fuel", "Combustible", "Combustível", "Carburant", "Carburante", "Brennstoff", "燃料", "燃料"]), brewing.fuel)); }
        }
        BlockEntity::Jukebox(jukebox) => {
            if let Some(record) = jukebox.record() {
                let id = record.item.as_ref(); let short = id.split_once(':').map_or(id, |(_, p)| p);
                out.push(format!("{}: {}", tr(["Record", "Disco", "Disco", "Disque", "Disco", "Schallplatte", "レコード", "唱片"]), stack_name(game, record, short)));
                if jukebox.playing { out.push(format!("{}: {} s", tr(["Playing", "Reproduciendo", "Reproduzindo", "Lecture", "In riproduzione", "Wiedergabe", "再生中", "播放中"]), jukebox.ticks / 20)); }
            }
        }
        BlockEntity::Lectern(lectern) => {
            if let Some(book) = lectern.book.as_ref() {
                let id = book.item.as_ref(); let short = id.split_once(':').map_or(id, |(_, p)| p);
                out.push(format!("{}: {}", tr(["Book", "Libro", "Livro", "Livre", "Libro", "Buch", "本", "书"]), stack_name(game, book, short)));
                out.push(format!("{}: {}", tr(["Page", "Página", "Página", "Page", "Pagina", "Seite", "ページ", "页"]), lectern.page + 1));
            }
        }
        BlockEntity::Spawner(spawner) => {
            out.push(format!("{}: {} s", tr(["Spawn delay", "Retraso de aparición", "Atraso de geração", "Délai d’apparition", "Ritardo generazione", "Spawn-Verzögerung", "スポーン遅延", "生成延迟"]), spawner.spawn_delay.max(0) / 20));
            out.push(format!("{}: {}", tr(["Spawn count", "Cantidad", "Quantidade", "Nombre", "Quantità", "Spawn-Anzahl", "スポーン数", "生成数量"]), spawner.spawn_count));
        }
        BlockEntity::TrialSpawner(spawner) => {
            out.push(format!("{}: {}", tr(["Active mobs", "Mobs activos", "Mobs ativos", "Mobs actifs", "Mob attivi", "Aktive Kreaturen", "出現中のMob", "活跃生物"]), spawner.current_mobs.len()));
            out.push(format!("{}: {}", tr(["Total spawned", "Total generados", "Total gerados", "Total apparu", "Totale generati", "Insgesamt gespawnt", "累計スポーン", "累计生成"]), spawner.total_mobs_spawned));
            if spawner.is_ominous { out.push(tr(["Ominous", "Agorero", "Sinistro", "Sinistre", "Minaccioso", "Unheilvoll", "不吉", "不祥"]).to_owned()); }
        }
        BlockEntity::Vault(vault) => {
            out.push(format!("{}: {}", tr(["Rewarded players", "Jugadores recompensados", "Jogadores recompensados", "Joueurs récompensés", "Giocatori premiati", "Belohnte Spieler", "報酬済みプレイヤー", "已奖励玩家"]), vault.rewarded_players.len()));
            if let Some(stack) = vault.display_item.as_ref() {
                let id = stack.item.as_ref(); let short = id.split_once(':').map_or(id, |(_, p)| p);
                out.push(format!("{}: {}", tr(["Display item", "Objeto mostrado", "Item exibido", "Objet affiché", "Oggetto esposto", "Angezeigter Gegenstand", "表示アイテム", "展示物品"]), stack_name(game, stack, short)));
            }
        }
        BlockEntity::Beehive(hive) => {
            out.push(format!("{}: {}", tr(["Bees inside", "Abejas dentro", "Abelhas dentro", "Abeilles dedans", "Api all'interno", "Bienen im Nest", "中のミツバチ", "蜂巢中的蜜蜂"]), hive.bees.len()));
        }
        BlockEntity::Campfire(campfire) => {
            let cooking = count_items(&campfire.items);
            out.push(format!("{}: {}/4", tr(["Cooking slots", "Espacios cocinando", "Slots cozinhando", "Emplacements en cuisson", "Slot in cottura", "Kochplätze", "調理スロット", "烹饪槽位"]), cooking));
            for i in 0..campfire.items.len() {
                if let Some(stack) = campfire.items[i].as_ref() {
                    let id = stack.item.as_ref(); let short = id.split_once(':').map_or(id, |(_, p)| p);
                    let name = stack_name(game, stack, short);
                    let progress = if campfire.total[i] > 0 { campfire.progress[i] * 100 / campfire.total[i] } else { 0 };
                    out.push(format!("{name}: {}%", progress.clamp(0, 100)));
                }
            }
        }
        BlockEntity::Crafter(crafter) => {
            out.push(format!("{}: {}/9", tr(["Disabled slots", "Espacios desactivados", "Slots desativados", "Emplacements désactivés", "Slot disattivati", "Deaktivierte Plätze", "無効スロット", "禁用槽位"]), crafter.disabled.iter().filter(|v| **v).count()));
            if crafter.triggered || crafter.crafting_ticks > 0 { out.push(tr(["Crafting", "Fabricando", "Fabricando", "Fabrication", "Fabbricazione", "Herstellung", "クラフト中", "合成中"]).to_owned()); }
        }
        BlockEntity::Beacon(beacon) => {
            out.push(format!("{}: {}", tr(["Levels", "Niveles", "Níveis", "Niveaux", "Livelli", "Stufen", "レベル", "等级"]), beacon.levels));
            if let Some(effect) = beacon.primary.as_deref() { out.push(format!("{}: {}", tr(["Primary", "Primario", "Primário", "Primaire", "Primario", "Primär", "一次", "主效果"]), effect)); }
            if let Some(effect) = beacon.secondary.as_deref() { out.push(format!("{}: {}", tr(["Secondary", "Secundario", "Secundário", "Secondaire", "Secondario", "Sekundär", "二次", "副效果"]), effect)); }
        }
        BlockEntity::Sculk(sculk) => {
            out.push(format!("{}: {:?}", tr(["Type", "Tipo", "Tipo", "Type", "Tipo", "Typ", "種類", "类型"]), sculk.kind));
            if sculk.warning_level > 0 { out.push(format!("{}: {}", tr(["Warning", "Advertencia", "Aviso", "Avertissement", "Avviso", "Warnung", "警告", "警告"]), sculk.warning_level)); }
        }
        BlockEntity::CommandBlock(command) => {
            if !command.command.is_empty() { out.push(format!("{}: {}", tr(["Command", "Comando", "Comando", "Commande", "Comando", "Befehl", "コマンド", "命令"]), command.command.clone())); }
            out.push(format!("{}: {}", tr(["Success count", "Ejecuciones exitosas", "Execuções bem-sucedidas", "Réussites", "Esecuzioni riuscite", "Erfolgsanzahl", "成功回数", "成功次数"]), command.success_count));
        }
        BlockEntity::ChiseledBookshelf(shelf) => {
            out.push(format!("{}: {}/6", tr(["Books", "Libros", "Livros", "Livres", "Libri", "Bücher", "本", "书"]), count_items(&shelf.items)));
        }
        BlockEntity::Shelf(shelf) => {
            out.push(format!("{}: {}/3", tr(["Displayed items", "Objetos mostrados", "Itens exibidos", "Objets affichés", "Oggetti esposti", "Angezeigte Gegenstände", "表示アイテム", "展示物品"]), count_items(&shelf.items)));
        }
        BlockEntity::Sign(sign) => {
            let text = sign.front.lines.iter().filter(|line| !line.is_empty()).cloned().collect::<Vec<_>>().join(" / ");
            if !text.is_empty() { out.push(text); }
            if sign.waxed { out.push(tr(["Waxed", "Encerado", "Encerado", "Ciré", "Cerato", "Gewachst", "ワックス済み", "已涂蜡"]).to_owned()); }
        }
        BlockEntity::Banner(banner) => {
            out.push(format!("{}: {}", tr(["Patterns", "Diseños", "Padrões", "Motifs", "Motivi", "Muster", "模様", "图案"]), banner.layers.len()));
            if let Some(name) = banner.name.as_deref() { out.push(name.to_owned()); }
        }
        BlockEntity::CreakingHeart(heart) => {
            out.push(format!("{}: {}", tr(["Output signal", "Señal de salida", "Sinal de saída", "Signal de sortie", "Segnale in uscita", "Ausgangssignal", "出力信号", "输出信号"]), heart.output_signal));
            if heart.emitter > 0 { out.push(format!("{}: {} s", tr(["Hurt pulse", "Pulso de daño", "Pulso de dano", "Impulsion de dégâts", "Impulso danno", "Schadenspuls", "攻撃パルス", "伤害脉冲"]), heart.emitter / 20)); }
        }
        BlockEntity::Brushable(brushable) => {
            if let Some(stack) = brushable.item.as_ref() {
                let id = stack.item.as_ref(); let short = id.split_once(':').map_or(id, |(_, p)| p);
                out.push(format!("{}: {}", tr(["Hidden item", "Objeto oculto", "Item oculto", "Objet caché", "Oggetto nascosto", "Versteckter Gegenstand", "隠しアイテム", "隐藏物品"]), stack_name(game, stack, short)));
            }
        }
        BlockEntity::EndGateway(gateway) => {
            out.push(format!("{}: {} s", tr(["Age", "Antigüedad", "Idade", "Âge", "Età", "Alter", "経過時間", "时间"]), gateway.age / 20));
            if let Some(exit) = gateway.exit_portal { out.push(format!("Exit: {}, {}, {}", exit[0], exit[1], exit[2])); }
        }
        BlockEntity::PotentSulfur(sulfur) => {
            if sulfur.waiting_countdown > 0 { out.push(format!("{}: {} s", tr(["Next change", "Próximo cambio", "Próxima mudança", "Prochain changement", "Prossimo cambio", "Nächste Änderung", "次の変化", "下次变化"]), sulfur.waiting_countdown)); }
        }
        BlockEntity::Mod(mod_entity) => {
            out.push(format!("{}: {}", tr(["Provider", "Proveedor", "Provedor", "Fournisseur", "Fornitore", "Anbieter", "プロバイダー", "提供者"]), mod_entity.id));
            if !mod_entity.items.is_empty() { out.push(format!("{}: {}/{}", tr(["Stored slots", "Espacios ocupados", "Slots ocupados", "Emplacements utilisés", "Slot occupati", "Belegte Plätze", "使用中スロット", "已占用槽位"]), count_items(&mod_entity.items), mod_entity.items.len())); }
            if !mod_entity.data.is_empty() { out.push(format!("{}: {}", tr(["Data", "Datos", "Dados", "Données", "Dati", "Daten", "データ", "数据"]), mod_entity.data.iter().map(i32::to_string).collect::<Vec<_>>().join(", "))); }
        }
        _ => {}
    }
    out
}

fn theme_index(game: &super::Game<'_>) -> usize {
    match game.native_setting(ID, "theme").as_deref() {
        Some("light") => 1,
        Some("purple") => 2,
        Some("high_contrast") => 3,
        _ => 0,
    }
}

fn background_opacity(game: &super::Game<'_>) -> u8 {
    let percent = game.native_setting(ID, "text_background_opacity")
        .and_then(|value| value.parse::<u8>().ok())
        .unwrap_or(82)
        .clamp(20, 100);
    ((percent as u16 * 255) / 100) as u8
}

fn theme_colors(game: &super::Game<'_>) -> (u32, u32, u32) {
    let alpha = background_opacity(game);
    match theme_index(game) {
        1 => (
            horizon_client::hud::rgba(240, 240, 240, alpha),
            horizon_client::hud::rgba(16, 16, 16, 255),
            horizon_client::hud::rgba(64, 64, 64, 255),
        ),
        2 => (
            horizon_client::hud::rgba(37, 24, 53, alpha),
            horizon_client::hud::rgba(255, 238, 255, 255),
            horizon_client::hud::rgba(215, 184, 255, 255),
        ),
        3 => (
            horizon_client::hud::rgba(0, 0, 0, alpha),
            horizon_client::hud::rgba(255, 255, 255, 255),
            horizon_client::hud::rgba(255, 255, 0, 255),
        ),
        _ => (
            horizon_client::hud::rgba(16, 16, 16, alpha),
            horizon_client::hud::rgba(255, 255, 255, 255),
            horizon_client::hud::rgba(184, 184, 184, 255),
        ),
    }
}

fn theme_name(game: &super::Game<'_>) -> &'static str {
    match theme_index(game) {
        1 => tr(["Light", "Claro", "Claro", "Clair", "Chiaro", "Hell", "ライト", "浅色"]),
        2 => tr(["Purple", "Púrpura", "Roxo", "Violet", "Viola", "Lila", "紫", "紫色"]),
        3 => tr(["High contrast", "Alto contraste", "Alto contraste", "Contraste élevé", "Alto contrasto", "Hoher Kontrast", "高コントラスト", "高对比度"]),
        _ => tr(["Dark", "Oscuro", "Escuro", "Sombre", "Scuro", "Dunkel", "ダーク", "深色"]),
    }
}

fn draw_target_panel(
    game: &super::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    id: &str,
    title: &str,
    entity: bool,
    stack: Option<&horizon_client::items::ItemStack>,
    details: &[String],
) {
    let atlas = &game.gui.atlas;
    let (background, title_color, detail_color) = theme_colors(game);
    let has_icon = enabled(game, "show_target_icon", true)
        && icon_available(game, atlas, id, entity, stack);
    let text_x = if has_icon { 26.0 } else { 6.0 };
    let title_width = horizon_client::hud::HudBatch::text_width(atlas, title);
    let detail_width = details.iter().map(|line| horizon_client::hud::HudBatch::text_width(atlas, line)).fold(0.0_f32, f32::max);
    let panel_width = (title_width.max(detail_width) + if has_icon { 32.0 } else { 12.0 }).min((batch.width - 16.0).max(0.0));
    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
    let panel_y = 8.0;
    let panel_height = 17.0 + details.len() as f32 * 12.0;
    batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), background);
    if has_icon { let _ = draw_icon(game, batch, atlas, id, entity, stack, panel_x + 6.0, panel_y + 2.0); }
    batch.text(atlas, title, panel_x + text_x, panel_y + 4.0, title_color, true);
    for (index, detail) in details.iter().enumerate() {
        batch.text(atlas, detail, panel_x + text_x, panel_y + 16.0 + index as f32 * 11.0, detail_color, true);
    }
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
        tiers.iter().copied().find(|tool| {
            tier_correct(items, tool, block)
                || (*tool == "minecraft:shears" && shear_block(game, block))
        }).map(str::to_owned)
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

fn setting_default(key: &str) -> bool {
    !matches!(
        key,
        "show_coordinates" | "show_block_states" | "show_hardness" | "show_item_count"
            | "show_block_face" | "show_distance" | "show_entity_distance" | "show_entity_registry_id"
            | "show_relative_coordinates" | "show_crop_progress" | "show_redstone_info"
            | "show_note_block_info" | "show_tnt_stability" | "show_beehive_honey" | "show_block_amount"
            | "show_entity_armor" | "show_entity_age" | "show_entity_breeding" | "show_horse_stats"
            | "show_entity_effects" | "show_villager_profession"
            | "show_entity_equipment" | "show_animal_tame_status" | "show_block_entity_info"
    )
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
    option_label_with_default(game, key, label, setting_default(key))
}

fn save_enabled(game: &super::Game<'_>, key: &str, value: bool) {
    let stored = if value { "true" } else { "false" };
    game.native_set_setting(ID, key, stored);
    if game.native_setting(ID, key).as_deref() != Some(stored) {
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
        keys: vec![
            ["show_overlay".into(), tr(["Toggle Jade overlay", "Mostrar/ocultar HUD de Jade", "Alternar HUD do Jade", "Afficher/masquer Jade", "Mostra/nascondi HUD Jade", "Jade-HUD umschalten", "Jade HUD切替", "切换Jade提示框"]).into(), "key.keyboard.keypad_0".into()],
            ["show_details".into(), tr(["Toggle Jade details", "Mostrar/ocultar detalles de Jade", "Alternar detalhes do Jade", "Afficher/masquer les détails Jade", "Mostra/nascondi dettagli Jade", "Jade-Details umschalten", "Jade詳細切替", "切换Jade详情"]).into(), "key.keyboard.keypad_1".into()],
        ],
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
        "Show harvest tools", "Mostrar herramientas necesarias", "Mostrar ferramentas necessárias", "Afficher les outils requis",
        "Mostra strumenti necessari", "Benötigte Werkzeuge anzeigen", "必要な道具を表示", "显示所需工具",
    ]));
    buttons.push(option_label_with_default(game, "show_hardness", [
        "Show block hardness", "Mostrar dureza del bloque", "Mostrar dureza do bloco", "Afficher la dureté du bloc",
        "Mostra durezza blocco", "Blockhärte anzeigen", "ブロックの硬度を表示", "显示方块硬度",
    ], false));
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
    buttons.push(option_label_with_default(game, "show_item_count", [
        "Show item count", "Mostrar cantidad de objetos", "Mostrar quantidade de itens", "Afficher la quantité",
        "Mostra quantità oggetti", "Gegenstandsanzahl anzeigen", "アイテム数を表示", "显示物品数量",
    ], false));
    buttons.push(option_label(game, "display_armor_stands", [
        "Display armor stands", "Mostrar soportes para armaduras", "Exibir suportes de armadura", "Afficher les porte-armures",
        "Mostra supporti per armature", "Rüstungsständer anzeigen", "防具立てを表示", "显示盔甲架",
    ]));
    buttons.push(option_label(game, "show_armor_stand_equipment", [
        "Show armor stand equipment", "Mostrar equipo del soporte", "Mostrar equipamento do suporte", "Afficher l’équipement du porte-armure",
        "Mostra equipaggiamento supporto", "Ausrüstung des Ständers anzeigen", "防具立ての装備を表示", "显示盔甲架装备",
    ]));
    buttons.push(option_label(game, "show_block_face", [
        "Show block face", "Mostrar cara del bloque", "Mostrar face do bloco", "Afficher la face du bloc",
        "Mostra faccia del blocco", "Blockseite anzeigen", "ブロック面を表示", "显示方块面",
    ]));
    buttons.push(option_label(game, "show_distance", [
        "Show block distance", "Mostrar distancia al bloque", "Mostrar distância do bloco", "Afficher la distance du bloc",
        "Mostra distanza blocco", "Blockentfernung anzeigen", "ブロックまでの距離", "显示方块距离",
    ]));
    buttons.push(option_label_with_default(game, "show_entity_distance", [
        "Show entity distance", "Mostrar distancia de entidades", "Mostrar distância das entidades", "Afficher la distance des entités",
        "Mostra distanza entità", "Entfernung zu Entitäten anzeigen", "エンティティまでの距離", "显示实体距离",
    ], false));
    buttons.push(option_label_with_default(game, "show_entity_registry_id", [
        "Show entity registry ID", "Mostrar ID de entidad", "Mostrar ID da entidade", "Afficher l’identifiant de l’entité",
        "Mostra ID entità", "Entitäts-Registrierungs-ID anzeigen", "エンティティ登録ID", "显示实体注册ID",
    ], false));
    buttons.push(option_label_with_default(game, "show_relative_coordinates", [
        "Show relative coordinates", "Mostrar coordenadas relativas", "Mostrar coordenadas relativas", "Afficher les coordonnées relatives",
        "Mostra coordinate relative", "Relative Koordinaten anzeigen", "相対座標を表示", "显示相对坐标",
    ], false));
    buttons.push(option_label_with_default(game, "show_crop_progress", [
        "Show crop growth", "Mostrar crecimiento de cultivos", "Mostrar crescimento das plantações", "Afficher la croissance des cultures",
        "Mostra crescita colture", "Pflanzenwachstum anzeigen", "作物の成長を表示", "显示作物生长",
    ], false));
    buttons.push(option_label_with_default(game, "show_redstone_info", [
        "Show redstone info", "Mostrar información de redstone", "Mostrar informações de redstone", "Afficher les informations de redstone",
        "Mostra informazioni redstone", "Redstone-Informationen anzeigen", "レッドストーン情報", "显示红石信息",
    ], false));
    buttons.push(option_label_with_default(game, "show_note_block_info", [
        "Show note block info", "Mostrar información del bloque musical", "Mostrar informações do bloco musical", "Afficher les informations du bloc musical",
        "Mostra informazioni blocco musicale", "Notenblock-Informationen anzeigen", "音符ブロック情報", "显示音符盒信息",
    ], false));
    buttons.push(option_label_with_default(game, "show_tnt_stability", [
        "Show TNT stability", "Mostrar estabilidad de TNT", "Mostrar estabilidade da TNT", "Afficher la stabilité du TNT",
        "Mostra stabilità TNT", "TNT-Stabilität anzeigen", "TNTの安定性", "显示TNT稳定性",
    ], false));
    buttons.push(option_label_with_default(game, "show_beehive_honey", [
        "Show beehive honey", "Mostrar nivel de miel", "Mostrar nível de mel", "Afficher le niveau de miel",
        "Mostra livello miele", "Honigstand anzeigen", "ハチミツ量を表示", "显示蜂蜜等级",
    ], false));
    buttons.push(option_label_with_default(game, "show_block_amount", [
        "Show block amount", "Mostrar cantidad del bloque", "Mostrar quantidade do bloco", "Afficher la quantité du bloc",
        "Mostra quantità blocco", "Blockmenge anzeigen", "ブロックの数を表示", "显示方块数量",
    ], false));
    buttons.push(option_label_with_default(game, "show_entity_armor", [
        "Show entity armor", "Mostrar armadura de entidades", "Mostrar armadura das entidades", "Afficher l’armure des entités",
        "Mostra armatura entità", "Entitätsrüstung anzeigen", "エンティティの防具", "显示实体护甲",
    ], false));
    buttons.push(option_label_with_default(game, "show_entity_age", [
        "Show entity age", "Mostrar edad de entidades", "Mostrar idade das entidades", "Afficher l’âge des entités",
        "Mostra età entità", "Entitätsalter anzeigen", "エンティティの年齢", "显示实体年龄",
    ], false));
    buttons.push(option_label_with_default(game, "show_entity_breeding", [
        "Show breeding status", "Mostrar estado de reproducción", "Mostrar estado de reprodução", "Afficher l’état de reproduction",
        "Mostra stato riproduzione", "Zuchtstatus anzeigen", "繁殖状態を表示", "显示繁殖状态",
    ], false));
    buttons.push(option_label_with_default(game, "show_horse_stats", [
        "Show horse stats", "Mostrar estadísticas de caballos", "Mostrar estatísticas dos cavalos", "Afficher les statistiques des chevaux",
        "Mostra statistiche cavalli", "Pferdewerte anzeigen", "馬の能力値", "显示马匹属性",
    ], false));
    buttons.push(option_label_with_default(game, "show_entity_effects", [
        "Show entity effects", "Mostrar efectos de entidades", "Mostrar efeitos das entidades", "Afficher les effets des entités",
        "Mostra effetti entità", "Entitätseffekte anzeigen", "エンティティの効果", "显示实体效果",
    ], false));
    buttons.push(option_label_with_default(game, "show_villager_profession", [
        "Show villager profession", "Mostrar profesión del aldeano", "Mostrar profissão do aldeão", "Afficher le métier du villageois",
        "Mostra professione abitante", "Dorfbewohner-Beruf anzeigen", "村人の職業", "显示村民职业",
    ], false));
    buttons.push(option_label_with_default(game, "show_entity_equipment", [
        "Show entity equipment", "Mostrar equipo de entidades", "Mostrar equipamento das entidades", "Afficher l’équipement des entités",
        "Mostra equipaggiamento entità", "Entitätsausrüstung anzeigen", "エンティティ装備", "显示实体装备",
    ], false));
    buttons.push(option_label_with_default(game, "show_animal_tame_status", [
        "Show tame status", "Mostrar estado de domesticación", "Mostrar estado de domesticação", "Afficher l’état d’apprivoisement",
        "Mostra stato addomesticamento", "Zähmungsstatus anzeigen", "懐き状態", "显示驯服状态",
    ], false));
    buttons.push(option_label(game, "display_item_frames", ["Display item frames", "Mostrar marcos", "Exibir molduras", "Afficher les cadres", "Mostra cornici", "Rahmen anzeigen", "額縁を表示", "显示物品展示框"]));
    buttons.push(option_label(game, "display_paintings", ["Display paintings", "Mostrar cuadros", "Exibir pinturas", "Afficher les tableaux", "Mostra dipinti", "Gemälde anzeigen", "絵画を表示", "显示画"]));
    buttons.push(option_label(game, "display_boats", ["Display boats", "Mostrar botes", "Exibir barcos", "Afficher les bateaux", "Mostra barche", "Boote anzeigen", "ボートを表示", "显示船"]));
    buttons.push(option_label(game, "display_minecarts", ["Display minecarts", "Mostrar vagonetas", "Exibir carrinhos", "Afficher les wagonnets", "Mostra carrelli", "Loren anzeigen", "トロッコを表示", "显示矿车"]));
    buttons.push(option_label(game, "display_falling_blocks", ["Display falling blocks", "Mostrar bloques en caída", "Exibir blocos em queda", "Afficher les blocs qui tombent", "Mostra blocchi in caduta", "Fallende Blöcke anzeigen", "落下ブロックを表示", "显示下落方块"]));
    buttons.push(option_label_with_default(game, "show_block_entity_info", ["Show block entity data", "Mostrar datos especiales del bloque", "Mostrar dados especiais do bloco", "Afficher les données du bloc", "Mostra dati speciali del blocco", "Blockentitätsdaten anzeigen", "ブロックエンティティ情報", "显示方块实体数据"], false));
    buttons.push(format!("{}: {}", tr(["Overlay theme", "Tema del HUD", "Tema do HUD", "Thème du HUD", "Tema HUD", "HUD-Design", "HUDテーマ", "提示框主题"]), theme_name(game)));
    let opacity = game.native_setting(ID, "text_background_opacity")
        .and_then(|value| value.parse::<u8>().ok())
        .unwrap_or(82)
        .clamp(20, 100);
    buttons.push(format!("{}: {}%", tr(["Background opacity", "Opacidad del fondo", "Opacidade do fundo", "Opacité du fond", "Opacità sfondo", "Hintergrunddeckkraft", "背景の不透明度", "背景不透明度"]), opacity));
}

pub fn settings_click(game: &mut super::Game<'_>, mod_id: &str, button: usize) {
    if mod_id != ID {
        return;
    }
    if button == 40 {
        let next = match theme_index(game) { 0 => "light", 1 => "purple", 2 => "high_contrast", _ => "dark" };
        game.native_set_setting(ID, "theme", next);
        if game.native_setting(ID, "theme").as_deref() != Some(next) {
            log::warn!("Jade: theme setting did not verify after writing");
        }
        return;
    }
    if button == 41 {
        let current = game.native_setting(ID, "text_background_opacity")
            .and_then(|value| value.parse::<u8>().ok())
            .unwrap_or(82);
        let next = match current {
            0..=39 => 40,
            40..=59 => 60,
            60..=79 => 80,
            80..=99 => 100,
            _ => 20,
        };
        let next_text = next.to_string();
        game.native_set_setting(ID, "text_background_opacity", &next_text);
        if game.native_setting(ID, "text_background_opacity").as_deref() != Some(next_text.as_str()) {
            log::warn!("Jade: background opacity setting did not verify after writing");
        }
        return;
    }
    let key = match button {
        0 => "display_tooltip",
        1 => "show_target_icon",
        2 => "show_registry_id",
        3 => "show_coordinates",
        4 => "show_block_states",
        5 => "show_block_info",
        6 => "show_hardness",
        7 => "show_item_mod_name",
        8 => "show_mod_name",
        9 => "display_entities",
        10 => "show_entity_health",
        11 => "display_dropped_items",
        12 => "show_item_count",
        13 => "display_armor_stands",
        14 => "show_armor_stand_equipment",
        15 => "show_block_face",
        16 => "show_distance",
        17 => "show_entity_distance",
        18 => "show_entity_registry_id",
        19 => "show_relative_coordinates",
        20 => "show_crop_progress",
        21 => "show_redstone_info",
        22 => "show_note_block_info",
        23 => "show_tnt_stability",
        24 => "show_beehive_honey",
        25 => "show_block_amount",
        26 => "show_entity_armor",
        27 => "show_entity_age",
        28 => "show_entity_breeding",
        29 => "show_horse_stats",
        30 => "show_entity_effects",
        31 => "show_villager_profession",
        32 => "show_entity_equipment",
        33 => "show_animal_tame_status",
        34 => "display_item_frames",
        35 => "display_paintings",
        36 => "display_boats",
        37 => "display_minecarts",
        38 => "display_falling_blocks",
        39 => "show_block_entity_info",
        _ => return,
    };
    let default = setting_default(key);
    let current = enabled(game, key, default);
    let next = !current;
    log::info!("Jade settings click received: button={button}, key={key}, current={current}, next={next}");
    save_enabled(game, key, next);
    let saved = enabled(game, key, default);
    log::info!("Jade setting after write: key={key}, expected={next}, actual={saved}");
}

pub fn on_command(game: &super::Game<'_>, command: &str, ran: &mut bool) {
    if !game.native_on(ID) {
        return;
    }
    let command = command.trim().strip_prefix('/').unwrap_or(command.trim());
    let mut parts = command.split_whitespace();
    if parts.next() != Some("jade") {
        return;
    }
    let action = parts.next().unwrap_or("toggle");
    match action {
        "show" | "on" => save_enabled(game, "display_tooltip", true),
        "hide" | "off" => save_enabled(game, "display_tooltip", false),
        "toggle" => save_enabled(game, "display_tooltip", !enabled(game, "display_tooltip", true)),
        "id" => if let Some(value) = parts.next() {
            if value == "on" || value == "true" { save_enabled(game, "show_registry_id", true); }
            else if value == "off" || value == "false" { save_enabled(game, "show_registry_id", false); }
        },
        "coordinates" | "coords" => if let Some(value) = parts.next() {
            if value == "on" || value == "true" { save_enabled(game, "show_coordinates", true); }
            else if value == "off" || value == "false" { save_enabled(game, "show_coordinates", false); }
        },
        "states" => if let Some(value) = parts.next() {
            if value == "on" || value == "true" { save_enabled(game, "show_block_states", true); }
            else if value == "off" || value == "false" { save_enabled(game, "show_block_states", false); }
        },
        "theme" => if let Some(value) = parts.next() {
            if matches!(value, "dark" | "light" | "purple" | "high_contrast") {
                game.native_set_setting(ID, "theme", value);
            }
        },
        "opacity" => if let Some(value) = parts.next() {
            if let Ok(percent) = value.parse::<u8>() {
                if (20..=100).contains(&percent) {
                    game.native_set_setting(ID, "text_background_opacity", &percent.to_string());
                }
            }
        },
        _ => {}
    }
    *ran = true;
}

pub fn on_key(game: &super::Game<'_>, key: &str, pressed: bool) {
    if !pressed || !game.native_on(ID) || game.player.is_none() {
        return;
    }
    let overlay_key = game.native_mod_key(ID, "show_overlay", "key.keyboard.keypad_0");
    if key == overlay_key {
        let next = !enabled(game, "display_tooltip", true);
        save_enabled(game, "display_tooltip", next);
        return;
    }
    let details_key = game.native_mod_key(ID, "show_details", "key.keyboard.keypad_1");
    if key == details_key {
        let next = !enabled(game, "show_registry_id", true);
        save_enabled(game, "show_registry_id", next);
    }
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
    let (panel_background, panel_title_color, panel_detail_color) = theme_colors(game);

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

    let frame_hit = if enabled(game, "display_item_frames", true) { game.frame_hit() } else { None };
    let painting_hit = if enabled(game, "display_paintings", true) { game.painting_hit() } else { None };
    let boat_hit = if enabled(game, "display_boats", true) {
        game.aim().and_then(|(eye, dir)| game.boats.iter().enumerate()
            .filter_map(|(i, boat)| super::ray_box(eye, dir, game.entity_reach, boat.bounds()).map(|d| (i, d)))
            .min_by(|a, b| a.1.total_cmp(&b.1)))
    } else { None };
    let cart_hit = if enabled(game, "display_minecarts", true) {
        game.aim().and_then(|(eye, dir)| game.carts.iter().enumerate()
            .filter_map(|(i, cart)| super::ray_box(eye, dir, game.entity_reach, cart.bounds()).map(|d| (i, d)))
            .min_by(|a, b| a.1.total_cmp(&b.1)))
    } else { None };
    let falling_hit = if enabled(game, "display_falling_blocks", true) {
        game.aim().and_then(|(eye, dir)| game.falling.iter().enumerate().filter_map(|(i, falling)| {
            let p = falling.position;
            let bounds = horizon_core::geometry::Aabb::new(
                horizon_core::geometry::Vec3::new(p.x - 0.49, p.y, p.z - 0.49),
                horizon_core::geometry::Vec3::new(p.x + 0.49, p.y + 0.98, p.z + 0.49),
            );
            super::ray_box(eye, dir, game.entity_reach, bounds).map(|d| (i, d))
        }).min_by(|a, b| a.1.total_cmp(&b.1)))
    } else { None };
    let nearest_target_distance = [
        if enabled(game, "display_entities", true) { game.mob_hit().map(|(_, d)| d) } else { None },
        item_hit.map(|(_, d)| d), armor_stand_hit.map(|(_, d)| d),
        frame_hit.map(|(_, d)| d), painting_hit.map(|(_, d)| d), boat_hit.map(|(_, d)| d),
        cart_hit.map(|(_, d)| d), falling_hit.map(|(_, d)| d),
    ].into_iter().flatten().fold(f64::INFINITY, f64::min);

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
                && distance <= nearest_target_distance + 0.0001
            {
                if let Some(mob) = game.mobs.get(index) {
                    let id: &str = mob.kind.id.as_str();
                    let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                    let title = mob.custom_name.clone().unwrap_or_else(|| {
                        let (namespace, path) = id.split_once(':').unwrap_or(("minecraft", id));
                        game.translate(&format!("entity.{namespace}.{path}"), path)
                    });
                    let mut details = Vec::with_capacity(4);
                    if enabled(game, "show_entity_registry_id", false) {
                        details.push(id.to_owned());
                    }
                    if enabled(game, "show_entity_distance", false) {
                        details.push(format!("{}: {:.1} m", tr([
                            "Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离",
                        ]), distance));
                    }
                    if enabled(game, "show_entity_armor", false) && mob.kind.armor > 0.0 {
                        details.push(format!("{}: {:.0}", tr([
                            "Armor", "Armadura", "Armadura", "Armure", "Armatura", "Rüstung", "防具", "护甲",
                        ]), mob.kind.armor));
                    }
                    if enabled(game, "show_entity_age", false)
                        && matches!(id,
                            "minecraft:pig" | "minecraft:cow" | "minecraft:mooshroom" | "minecraft:sheep"
                            | "minecraft:chicken" | "minecraft:rabbit" | "minecraft:horse" | "minecraft:donkey"
                            | "minecraft:mule" | "minecraft:llama" | "minecraft:trader_llama" | "minecraft:goat"
                            | "minecraft:panda" | "minecraft:polar_bear" | "minecraft:fox" | "minecraft:wolf"
                            | "minecraft:cat" | "minecraft:ocelot" | "minecraft:parrot" | "minecraft:bee"
                            | "minecraft:turtle" | "minecraft:armadillo" | "minecraft:frog" | "minecraft:axolotl"
                            | "minecraft:dolphin" | "minecraft:camel" | "minecraft:sniffer" | "minecraft:happy_ghast"
                        )
                    {
                        if mob.extra.age < -1 {
                            let remaining = mob.extra.age.saturating_neg();
                            let time = format!("{}:{:02}", remaining / 1200, (remaining / 20) % 60);
                            details.push(if mob.extra.age_locked {
                                tr(["Growth paused", "Crecimiento pausado", "Crescimento pausado", "Croissance en pause", "Crescita in pausa", "Wachstum pausiert", "成長停止中", "生长暂停"]).to_owned()
                            } else {
                                format!("{}: {time}", tr(["Growing time", "Tiempo de crecimiento", "Tempo de crescimento", "Temps de croissance", "Tempo di crescita", "Wachstumszeit", "成長時間", "生长时间"]))
                            });
                        } else if mob.extra.age == -1 {
                            details.push(tr(["Baby", "Bebé", "Filhote", "Bébé", "Cucciolo", "Baby", "子ども", "幼年"]).to_owned());
                        }
                    }
                    if enabled(game, "show_entity_breeding", false) {
                        if id == "minecraft:allay" && mob.extra.egg_time > 0 {
                            details.push(tr(["Duplication cooldown", "Enfriamiento de duplicación", "Recarga de duplicação", "Recharge de duplication", "Ricarica duplicazione", "Duplizierungs-Abklingzeit", "複製クールダウン", "复制冷却"]).to_owned());
                        } else if mob.extra.in_love > 0 {
                            details.push(format!("{}: {} s", tr([
                                "Breeding", "Reproducción", "Reprodução", "Reproduction", "Riproduzione", "Zucht", "繁殖", "繁殖",
                            ]), mob.extra.in_love / 20));
                        } else if mob.extra.age > 0 {
                            let remaining = mob.extra.age;
                            let time = format!("{}:{:02}", remaining / 1200, (remaining / 20) % 60);
                            details.push(format!("{}: {time}", tr([
                                "Breeding cooldown", "Enfriamiento de reproducción", "Recarga de reprodução", "Recharge de reproduction", "Ricarica riproduzione", "Zucht-Abklingzeit", "繁殖クールダウン", "繁殖冷却",
                            ])));
                        }
                    }
                    if enabled(game, "show_horse_stats", false)
                        && matches!(id, "minecraft:horse" | "minecraft:donkey" | "minecraft:mule" | "minecraft:skeleton_horse" | "minecraft:zombie_horse")
                    {
                        let jump = 4.53680079 * mob.attrs.jump_strength * mob.attrs.jump_strength
                            + 1.6143173 * mob.attrs.jump_strength - 0.22656224;
                        let speed = mob.attrs.speed * 43.17181546666666 - 3.39999999E-7;
                        details.push(format!("{}: {:.2} m", tr([
                            "Jump", "Salto", "Salto", "Saut", "Salto", "Sprung", "跳躍", "跳跃",
                        ]), jump));
                        details.push(format!("{}: {:.2} m/s", tr([
                            "Speed", "Velocidad", "Velocidade", "Vitesse", "Velocità", "Geschwindigkeit", "速度", "速度",
                        ]), speed));
                    }
                    if enabled(game, "show_entity_effects", false) {
                        for (effect, ticks, amplifier) in mob.extra.effects.iter().take(3) {
                            let short = effect.strip_prefix("minecraft:").unwrap_or(effect);
                            let mut effect_name = game.translate(&format!("effect.minecraft.{short}"), short);
                            if *amplifier > 0 {
                                effect_name.push_str(&format!(" {}", amplifier + 1));
                            }
                            let duration = if *ticks < 0 {
                                "∞".to_owned()
                            } else {
                                format!("{}:{:02}", ticks / 1200, (ticks / 20) % 60)
                            };
                            details.push(format!("{effect_name} ({duration})"));
                        }
                    }
                    if enabled(game, "show_villager_profession", false)
                        && matches!(id, "minecraft:villager" | "minecraft:zombie_villager")
                    {
                        const PROFESSIONS: [&str; 15] = [
                            "none", "armorer", "butcher", "cartographer", "cleric", "farmer", "fisherman",
                            "fletcher", "leatherworker", "librarian", "mason", "nitwit", "shepherd", "toolsmith", "weaponsmith",
                        ];
                        let profession = PROFESSIONS[mob.extra.variant2 as usize % PROFESSIONS.len()];
                        let profession_name = game.translate(&format!("entity.minecraft.villager.{profession}"), profession);
                        let level = mob.extra.variant3;
                        let level_name = match level {
                            1 => tr(["Novice", "Principiante", "Novato", "Novice", "Principiante", "Anfänger", "新米", "新手"]),
                            2 => tr(["Apprentice", "Aprendiz", "Aprendiz", "Apprenti", "Apprendista", "Lehrling", "見習い", "学徒"]),
                            3 => tr(["Journeyman", "Oficial", "Profissional", "Compagnon", "Qualificato", "Geselle", "熟練者", "熟练工"]),
                            4 => tr(["Expert", "Experto", "Especialista", "Expert", "Esperto", "Experte", "専門家", "专家"]),
                            5 => tr(["Master", "Maestro", "Mestre", "Maître", "Maestro", "Meister", "達人", "大师"]),
                            _ => "",
                        };
                        details.push(if level_name.is_empty() {
                            profession_name
                        } else {
                            format!("{profession_name} - {level_name}")
                        });
                    }
                    if enabled(game, "show_entity_equipment", false) {
                        let mut equipment = Vec::new();
                        for stack in mob.extra.armor.iter().flatten() {
                            let short = stack.item.split_once(':').map_or(stack.item.as_ref(), |(_, path)| path);
                            equipment.push(stack_name(game, stack, short));
                        }
                        for (label, held) in [
                            (tr(["Main hand", "Mano principal", "Mão principal", "Main", "Mano principale", "Haupthand", "メインハンド", "主手"]), mob.extra.main_hand.as_deref()),
                            (tr(["Off hand", "Mano secundaria", "Mão secundária", "Seconde main", "Mano secondaria", "Nebenhand", "オフハンド", "副手"]), mob.extra.off_hand.as_deref()),
                        ] {
                            if let Some(item) = held {
                                let short = item.split_once(':').map_or(item, |(_, path)| path);
                                equipment.push(format!("{label}: {}", game.translate(&format!("item.minecraft.{short}"), short)));
                            }
                        }
                        if !equipment.is_empty() {
                            details.push(format!("{}: {}", tr(["Equipment", "Equipo", "Equipamento", "Équipement", "Equipaggiamento", "Ausrüstung", "装備", "装备"]), equipment.join(", ")));
                        }
                    }
                    if enabled(game, "show_animal_tame_status", false)
                        && matches!(id, "minecraft:wolf" | "minecraft:cat" | "minecraft:parrot" | "minecraft:horse" | "minecraft:donkey" | "minecraft:mule" | "minecraft:llama" | "minecraft:trader_llama")
                    {
                        let status = if mob.extra.owner.is_some() {
                            tr(["Tamed", "Domesticado", "Domesticado", "Apprivoisé", "Addomesticato", "Gezähmt", "懐いている", "已驯服"])
                        } else {
                            tr(["Wild", "Salvaje", "Selvagem", "Sauvage", "Selvatico", "Wild", "野生", "野生"])
                        };
                        details.push(status.to_owned());
                    }
                    if enabled(game, "show_mod_name", true) {
                        if let Some((namespace, _)) = id.split_once(':') {
                            if namespace != "minecraft" && !namespace.is_empty() {
                                details.push(format!("{}: {}", tr([
                                    "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "Mod", "模组",
                                ]), namespace));
                            }
                        }
                    }
                    let health_line = enabled(game, "show_entity_health", true).then(|| {
                        format!("{:.1}/{:.1}", mob.health.max(0.0), mob.attrs.max_health.max(1.0))
                    });
                    let atlas = &game.gui.atlas;
                    let has_icon = enabled(game, "show_target_icon", true)
                        && icon_available(game, atlas, id, true, None);
                    let text_x = if has_icon { 26.0 } else { 6.0 };
                    let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
                    let detail_width = details.iter()
                        .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
                        .chain(health_line.iter().map(|line| horizon_client::hud::HudBatch::text_width(atlas, &format!("♥ {line}"))))
                        .fold(0.0_f32, f32::max);
                    let panel_width = (title_width.max(detail_width) + if has_icon { 32.0 } else { 12.0 })
                        .min((batch.width - 16.0).max(0.0));
                    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
                    let panel_y = 8.0;
                    let panel_height = 17.0 + (details.len() + usize::from(health_line.is_some())) as f32 * 12.0;
                    batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), panel_background);
                    if has_icon {
                        let _ = draw_icon(game, batch, atlas, id, true, None, panel_x + 6.0, panel_y + 2.0);
                    }
                    batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, panel_title_color, true);
                    for (line_index, detail) in details.iter().enumerate() {
                        batch.text(
                            atlas,
                            detail,
                            panel_x + text_x,
                            panel_y + 16.0 + line_index as f32 * 11.0,
                            panel_detail_color,
                            true,
                        );
                    }
                    if let Some(health) = health_line {
                        let y = panel_y + 16.0 + details.len() as f32 * 11.0;
                        batch.text(atlas, "♥", panel_x + text_x, y, horizon_client::hud::rgba(255, 0, 0, 255), true);
                        batch.text(atlas, &health, panel_x + text_x + 9.0, y, panel_detail_color, true);
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
            && distance <= nearest_target_distance + 0.0001
        {
            if let Some(item) = game.dropped.get(index) {
                let id = item.stack.item.as_ref();
                let short_name = id.split_once(':').map_or(id, |(_, name)| name);
                let title = stack_name(game, &item.stack, short_name);
                let mut details = Vec::with_capacity(4);
                if enabled(game, "show_entity_distance", false) {
                    details.push(format!("{}: {:.1} m", tr([
                        "Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离",
                    ]), distance));
                }
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
                if enabled(game, "show_item_count", false) {
                    details.push(format!("{}: {}", tr([
                        "Count", "Cantidad", "Quantidade", "Quantité", "Quantità", "Anzahl", "個数", "数量",
                    ]), item.stack.count));
                }
                let atlas = &game.gui.atlas;
                let has_icon = enabled(game, "show_target_icon", true)
                    && icon_available(game, atlas, id, false, Some(&item.stack));
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
                batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), panel_background);
                if has_icon {
                    let _ = draw_icon(game, batch, atlas, id, false, Some(&item.stack), panel_x + 6.0, panel_y + 2.0);
                }
                batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, panel_title_color, true);
                for (line_index, detail) in details.iter().enumerate() {
                    batch.text(
                        atlas,
                        detail,
                        panel_x + text_x,
                        panel_y + 16.0 + line_index as f32 * 11.0,
                        panel_detail_color,
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
            && distance <= nearest_target_distance + 0.0001
        {
            if let Some(stand) = game.armor_stands.get(index) {
                let title = game.translate("entity.minecraft.armor_stand", "Armor Stand");
                let mut details = Vec::new();
                if enabled(game, "show_entity_registry_id", false) {
                    details.push("minecraft:armor_stand".to_owned());
                }
                if enabled(game, "show_entity_distance", false) {
                    details.push(format!("{}: {:.1} m", tr([
                        "Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离",
                    ]), distance));
                }
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
                    && icon_available(game, atlas, "minecraft:armor_stand", true, None);
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
                batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), panel_background);
                if has_icon {
                    let _ = draw_icon(game, batch, atlas, "minecraft:armor_stand", true, None, panel_x + 6.0, panel_y + 2.0);
                }
                batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, panel_title_color, true);
                for (line_index, detail) in details.iter().enumerate() {
                    batch.text(
                        atlas,
                        detail,
                        panel_x + text_x,
                        panel_y + 16.0 + line_index as f32 * 11.0,
                        panel_detail_color,
                        true,
                    );
                }
                return;
            }
        }
    }

    if let Some((index, distance)) = frame_hit {
        let block_distance = game.aim().and_then(|(eye, _)| game.pick_unfiltered().map(|b| (0..3).map(|a| (b.point[a] - eye[a]).powi(2)).sum::<f64>().sqrt()));
        if block_distance.is_none_or(|d| distance < d) && distance <= nearest_target_distance + 0.0001 {
            if let Some(frame) = game.item_frames.get(index) {
                let (title, stack) = if let Some(stack) = frame.item.as_ref() {
                    let id = stack.item.as_ref(); let short = id.split_once(':').map_or(id, |(_, p)| p);
                    (stack_name(game, stack, short), Some(stack))
                } else {
                    let fallback = if frame.glow { "Glow Item Frame" } else { "Item Frame" };
                    let key = if frame.glow { "entity.minecraft.glow_item_frame" } else { "entity.minecraft.item_frame" };
                    (game.translate(key, fallback), None)
                };
                let mut details = Vec::new();
                if enabled(game, "show_entity_registry_id", false) { details.push(if frame.glow { "minecraft:glow_item_frame".into() } else { "minecraft:item_frame".into() }); }
                if enabled(game, "show_entity_distance", false) { details.push(format!("{}: {:.1} m", tr(["Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离"]), distance)); }
                if let Some(stack) = stack {
                    details.push(format!("{}: {}", tr(["Displayed item", "Objeto mostrado", "Item exibido", "Objet affiché", "Oggetto esposto", "Angezeigter Gegenstand", "表示アイテム", "展示物品"]), stack_name(game, stack, stack.item.as_ref())));
                    details.push(format!("{}: {}/8", tr(["Rotation", "Rotación", "Rotação", "Rotation", "Rotazione", "Drehung", "回転", "旋转"]), frame.rotation));
                }
                draw_target_panel(game, batch, stack.map_or("minecraft:item_frame", |s| s.item.as_ref()), &title, false, stack, &details);
                return;
            }
        }
    }

    if let Some((index, distance)) = painting_hit {
        let block_distance = game.aim().and_then(|(eye, _)| game.pick_unfiltered().map(|b| (0..3).map(|a| (b.point[a] - eye[a]).powi(2)).sum::<f64>().sqrt()));
        if block_distance.is_none_or(|d| distance < d) && distance <= nearest_target_distance + 0.0001 {
            if let Some(painting) = game.paintings.get(index) {
                let variant = painting.variant.as_ref(); let short = variant.split_once(':').map_or(variant, |(_, p)| p);
                let title = game.translate(&format!("painting.minecraft.{short}"), short);
                let mut details = Vec::new();
                if enabled(game, "show_entity_registry_id", false) { details.push(variant.to_owned()); }
                if enabled(game, "show_entity_distance", false) { details.push(format!("{}: {:.1} m", tr(["Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离"]), distance)); }
                details.push(format!("{}: {}×{}", tr(["Size", "Tamaño", "Tamanho", "Taille", "Dimensione", "Größe", "サイズ", "尺寸"]), painting.width, painting.height));
                draw_target_panel(game, batch, "minecraft:painting", &title, false, None, &details);
                return;
            }
        }
    }

    if let Some((index, distance)) = boat_hit {
        let block_distance = game.aim().and_then(|(eye, _)| game.pick_unfiltered().map(|b| (0..3).map(|a| (b.point[a] - eye[a]).powi(2)).sum::<f64>().sqrt()));
        if block_distance.is_none_or(|d| distance < d) && distance <= nearest_target_distance + 0.0001 {
            if let Some(boat) = game.boats.get(index) {
                let id = if boat.chest { format!("minecraft:{}_chest_boat", boat.wood) } else { format!("minecraft:{}_boat", boat.wood) };
                let path = id.split_once(':').map_or(id.as_str(), |(_, p)| p);
                let title = game.translate(&format!("item.minecraft.{path}"), path);
                let mut details = Vec::new();
                if enabled(game, "show_entity_registry_id", false) { details.push(id.clone()); }
                if enabled(game, "show_entity_distance", false) { details.push(format!("{}: {:.1} m", tr(["Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离"]), distance)); }
                if boat.chest {
                    details.push(tr(["Has chest", "Tiene cofre", "Tem baú", "Avec coffre", "Con cassa", "Mit Truhe", "チェスト付き", "带箱子"]).to_owned());
                    let occupied = boat.items.iter().flatten().count();
                    details.push(format!("{}: {}/{}", tr(["Stored slots", "Espacios ocupados", "Slots ocupados", "Emplacements utilisés", "Slot occupati", "Belegte Plätze", "使用中スロット", "已占用槽位"]), occupied, boat.items.len()));
                    for stack in boat.items.iter().flatten().take(3) {
                        let item_id = stack.item.as_ref();
                        let short = item_id.split_once(':').map_or(item_id, |(_, p)| p);
                        details.push(stack_name(game, stack, short));
                    }
                }
                draw_target_panel(game, batch, &id, &title, false, None, &details);
                return;
            }
        }
    }

    if let Some((index, distance)) = cart_hit {
        let block_distance = game.aim().and_then(|(eye, _)| game.pick_unfiltered().map(|b| (0..3).map(|a| (b.point[a] - eye[a]).powi(2)).sum::<f64>().sqrt()));
        if block_distance.is_none_or(|d| distance < d) && distance <= nearest_target_distance + 0.0001 {
            if let Some(cart) = game.carts.get(index) {
                let id = cart.kind.id(); let item = cart.kind.item();
                let path = item.split_once(':').map_or(item, |(_, p)| p);
                let title = game.translate(&format!("item.minecraft.{path}"), path);
                let mut details = Vec::new();
                if enabled(game, "show_entity_registry_id", false) { details.push(id.to_owned()); }
                if enabled(game, "show_entity_distance", false) { details.push(format!("{}: {:.1} m", tr(["Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离"]), distance)); }
                if cart.fuse >= 0 { details.push(format!("{}: {} s", tr(["Fuse", "Mecha", "Pavio", "Mèche", "Miccia", "Zünder", "導火線", "引信"]), cart.fuse / 20)); }
                if cart.fuel > 0 { details.push(format!("{}: {} s", tr(["Fuel", "Combustible", "Combustível", "Carburant", "Carburante", "Treibstoff", "燃料", "燃料"]), cart.fuel / 20)); }
                if !cart.items.is_empty() {
                    let occupied = cart.items.iter().flatten().count();
                    details.push(format!("{}: {}/{}", tr(["Stored slots", "Espacios ocupados", "Slots ocupados", "Emplacements utilisés", "Slot occupati", "Belegte Plätze", "使用中スロット", "已占用槽位"]), occupied, cart.items.len()));
                    for stack in cart.items.iter().flatten().take(3) {
                        let item_id = stack.item.as_ref();
                        let short = item_id.split_once(':').map_or(item_id, |(_, p)| p);
                        details.push(stack_name(game, stack, short));
                    }
                }
                draw_target_panel(game, batch, item, &title, false, None, &details);
                return;
            }
        }
    }

    if let Some((index, distance)) = falling_hit {
        let block_distance = game.aim().and_then(|(eye, _)| game.pick_unfiltered().map(|b| (0..3).map(|a| (b.point[a] - eye[a]).powi(2)).sum::<f64>().sqrt()));
        if block_distance.is_none_or(|d| distance < d) && distance <= nearest_target_distance + 0.0001 {
            if let Some(falling) = game.falling.get(index) {
                if let Some(state) = game.pack.assets.registry.state(falling.state) {
                    let id = state.name.as_str();
                    let short = id.split_once(':').map_or(id, |(_, p)| p);
                    let title = game.translate(&format!("block.{}", id.replace(':', ".")), short);
                    let mut details = Vec::new();
                    if enabled(game, "show_entity_registry_id", false) { details.push(id.to_owned()); }
                    if enabled(game, "show_entity_distance", false) { details.push(format!("{}: {:.1} m", tr(["Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离"]), distance)); }
                    draw_target_panel(game, batch, id, &title, false, None, &details);
                    return;
                }
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

    let facts = game.res.facts().get(state);
    let property = |key: &str| {
        block_state.properties.iter()
            .find(|(name, _)| name.as_str() == key)
            .map(|(_, value)| value.as_str())
    };
    let mut details = Vec::with_capacity(12);
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
    if enabled(game, "show_coordinates", false) {
        details.push(format!("X: {}  Y: {}  Z: {}", hit.pos[0], hit.pos[1], hit.pos[2]));
    }
    if enabled(game, "show_relative_coordinates", false) {
        if let Some(player) = game.player.as_ref() {
            let origin = player.position;
            details.push(format!("{}: {:.1}  {:.1}  {:.1}", tr([
                "Relative", "Relativas", "Relativas", "Relatives", "Relative", "Relativ", "相対", "相对",
            ]), hit.pos[0] as f64 + 0.5 - origin.x, hit.pos[1] as f64 + 0.5 - origin.y, hit.pos[2] as f64 + 0.5 - origin.z));
        }
    }
    if enabled(game, "show_block_face", false) {
        let face = match hit.face {
            [0, 1, 0] => tr(["Top", "Arriba", "Cima", "Haut", "Alto", "Oben", "上", "上方"]),
            [0, -1, 0] => tr(["Bottom", "Abajo", "Baixo", "Bas", "Basso", "Unten", "下", "下方"]),
            [1, 0, 0] => tr(["East", "Este", "Leste", "Est", "Est", "Osten", "東", "东"]),
            [-1, 0, 0] => tr(["West", "Oeste", "Oeste", "Ouest", "Ovest", "Westen", "西", "西"]),
            [0, 0, 1] => tr(["South", "Sur", "Sul", "Sud", "Sud", "Süden", "南", "南"]),
            [0, 0, -1] => tr(["North", "Norte", "Norte", "Nord", "Nord", "Norden", "北", "北"]),
            _ => tr(["Unknown", "Desconocida", "Desconhecida", "Inconnue", "Sconosciuta", "Unbekannt", "不明", "未知"]),
        };
        details.push(format!("{}: {face}", tr(["Face", "Cara", "Face", "Face", "Faccia", "Seite", "面", "面"])));
    }
    if enabled(game, "show_distance", false) {
        if let Some((eye, _)) = game.aim() {
            let distance = (0..3)
                .map(|axis| (hit.point[axis] - eye[axis]).powi(2))
                .sum::<f64>()
                .sqrt();
            details.push(format!("{}: {:.1} m", tr(["Distance", "Distancia", "Distância", "Distance", "Distanza", "Entfernung", "距離", "距离"]), distance));
        }
    }
    if enabled(game, "show_block_states", false) && !block_state.properties.is_empty() {
        let properties = block_state.properties.iter()
            .map(|(key, value)| format!("{key}={value}"))
            .collect::<Vec<_>>()
            .join(", ");
        details.push(format!("{}: {}", tr([
            "State", "Estado", "Estado", "État", "Stato", "Zustand", "状態", "状态",
        ]), properties));
    }
    if enabled(game, "show_hardness", false) {
        if facts.hardness < 0.0 {
            details.push(tr(["Unbreakable", "Irrompible", "Inquebrável", "Incassable", "Indistruttibile", "Unzerstörbar", "破壊不可", "不可破坏"]).to_owned());
        } else {
            details.push(format!("{}: {:.1}", tr(["Hardness", "Dureza", "Dureza", "Dureté", "Durezza", "Härte", "硬度", "硬度"]), facts.hardness));
        }
    }
    if enabled(game, "show_crop_progress", false) {
        if let Some(age) = property("age").and_then(|value| value.parse::<u32>().ok()) {
            let maximum = match name {
                "minecraft:beetroots" => 3,
                "minecraft:cocoa" => 2,
                "minecraft:nether_wart" => 3,
                "minecraft:sweet_berry_bush" => 3,
                "minecraft:torchflower_crop" => 2,
                "minecraft:pitcher_crop" => 4,
                _ if name.ends_with("_stem") => 7,
                _ => 7,
            };
            let crop = name.ends_with("_crop") || name.ends_with("_stem") || matches!(name,
                "minecraft:wheat" | "minecraft:carrots" | "minecraft:potatoes" | "minecraft:beetroots"
                | "minecraft:nether_wart" | "minecraft:sweet_berry_bush" | "minecraft:cocoa"
                | "minecraft:torchflower_crop" | "minecraft:pitcher_crop");
            if crop {
                let percent = (age.min(maximum) * 100) / maximum.max(1);
                details.push(if age >= maximum {
                    tr(["Mature", "Maduro", "Madura", "Mûr", "Maturo", "Ausgereift", "成熟", "成熟"]).to_owned()
                } else {
                    format!("{}: {}%", tr(["Growth", "Crecimiento", "Crescimento", "Croissance", "Crescita", "Wachstum", "成長", "生长"]), percent)
                });
            }
        }
    }
    if enabled(game, "show_redstone_info", false) {
        if name == "minecraft:lever" {
            let on = property("powered") == Some("true");
            details.push(format!("{}: {}", tr(["State", "Estado", "Estado", "État", "Stato", "Status", "状態", "状态"]),
                if on { tr(["On", "Activado", "Ativado", "Activé", "Attivo", "Ein", "オン", "开启"]) }
                else { tr(["Off", "Desactivado", "Desativado", "Désactivé", "Disattivo", "Aus", "オフ", "关闭"]) }));
        } else if name == "minecraft:repeater" {
            if let Some(delay) = property("delay") {
                details.push(format!("{}: {}", tr(["Delay", "Retardo", "Atraso", "Délai", "Ritardo", "Verzögerung", "遅延", "延迟"]), delay));
            }
        } else if name == "minecraft:comparator" {
            if let Some(mode) = property("mode") {
                details.push(format!("{}: {}", tr(["Mode", "Modo", "Modo", "Mode", "Modalità", "Modus", "モード", "模式"]), mode));
            }
        } else if let Some(power) = property("power") {
            details.push(format!("{}: {}", tr(["Power", "Potencia", "Potência", "Puissance", "Potenza", "Stärke", "信号強度", "信号强度"]), power));
        }
    }
    if enabled(game, "show_note_block_info", false) && name == "minecraft:note_block" {
        let instrument = property("instrument").unwrap_or("harp").replace('_', " ");
        let note = property("note").and_then(|value| value.parse::<usize>().ok()).unwrap_or(0).min(24);
        const NOTES: [&str; 12] = ["F♯", "G", "G♯", "A", "A♯", "B", "C", "C♯", "D", "D♯", "E", "F"];
        let octave = if note < 6 { 1 } else if note < 18 { 2 } else { 3 };
        details.push(format!("{}: {} - {}{}", tr(["Instrument", "Instrumento", "Instrumento", "Instrument", "Strumento", "Instrument", "楽器", "乐器"]), instrument, NOTES[note % 12], octave));
    }
    if enabled(game, "show_tnt_stability", false) && name == "minecraft:tnt" && property("unstable") == Some("true") {
        details.push(tr(["Unstable", "Inestable", "Instável", "Instable", "Instabile", "Instabil", "不安定", "不稳定"]).to_owned());
    }
    if enabled(game, "show_beehive_honey", false) {
        if let Some(level) = property("honey_level") {
            details.push(format!("{}: {}/5", tr(["Honey", "Miel", "Mel", "Miel", "Miele", "Honig", "ハチミツ", "蜂蜜"]), level));
        }
    }
    if enabled(game, "show_block_amount", false) {
        for (key, label) in [
            ("pickles", tr(["Pickles", "Pepinillos", "Picles", "Cornichons", "Cetrioli", "Gurken", "ピクルス", "泡菜"])),
            ("candles", tr(["Candles", "Velas", "Velas", "Bougies", "Candele", "Kerzen", "ろうそく", "蜡烛"])),
            ("eggs", tr(["Eggs", "Huevos", "Ovos", "Œufs", "Uova", "Eier", "卵", "鸡蛋"])),
            ("bites", tr(["Bites", "Mordiscos", "Mordidas", "Bouchées", "Morsi", "Bissen", "かじった回数", "咬痕"])),
            ("layers", tr(["Layers", "Capas", "Camadas", "Couches", "Strati", "Schichten", "層", "层"])),
        ] {
            if let Some(value) = property(key) {
                details.push(format!("{label}: {value}"));
                break;
            }
        }
    }
    if enabled(game, "show_block_entity_info", false) {
        details.extend(block_entity_details(game, hit.pos));
    }
    if enabled(game, "show_block_info", true) {
        // Jade's provider selects the lowest valid tier per tool type. This port
        // displays only the single fastest valid tool to keep the overlay compact.
        required_tools = harvest_tools(game, name);
        let items = game.res.items();
        required_tools.sort_by(|a, b| {
            let speed_a = items.mining_speed(
                Some(&horizon_client::items::ItemStack::new(a, 1)),
                name,
            );
            let speed_b = items.mining_speed(
                Some(&horizon_client::items::ItemStack::new(b, 1)),
                name,
            );
            speed_b.total_cmp(&speed_a)
        });
        required_tools.truncate(1);
    }
    let atlas = &game.gui.atlas;
    let has_icon = enabled(game, "show_target_icon", true)
        && icon_available(game, atlas, name, false, block_stack.as_ref());
    let text_x = if has_icon { 26.0 } else { 6.0 };
    let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
    let detail_width = details.iter()
        .map(|line| horizon_client::hud::HudBatch::text_width(atlas, line))
        .fold(0.0_f32, f32::max);
    let has_tool_row = enabled(game, "show_block_info", true) && !required_tools.is_empty();
    let show_tool_check = has_tool_row && facts.requires_tool;
    let tool_row_width = if has_tool_row {
        30.0 + if show_tool_check { 10.0 } else { 0.0 }
    } else {
        0.0
    };
    let panel_width = (title_width.max(detail_width).max(tool_row_width) + if has_icon { 32.0 } else { 12.0 })
        .min((batch.width - 16.0).max(0.0));
    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
    let panel_y = 8.0;
    let panel_height = 17.0 + details.len() as f32 * 12.0 + if has_tool_row { 19.0 } else { 0.0 };

    batch.fill(atlas, panel_x, panel_y, panel_width, panel_height.max(if has_icon { 20.0 } else { 0.0 }), panel_background);
    if has_icon {
        let _ = draw_icon(game, batch, atlas, name, false, block_stack.as_ref(), panel_x + 6.0, panel_y + 2.0);
    }
    batch.text(atlas, &title, panel_x + text_x, panel_y + 4.0, panel_title_color, true);
    for (index, detail) in details.iter().enumerate() {
        batch.text(
            atlas,
            detail,
            panel_x + text_x,
            panel_y + 16.0 + index as f32 * 11.0,
            panel_detail_color,
            true,
        );
    }
    if has_tool_row {
        let row_y = panel_y + 16.0 + details.len() as f32 * 11.0;
        let icon_x = panel_x + text_x;
        let _ = game.native_item(batch, &required_tools[0], [icon_x, row_y], 16.0);
        if show_tool_check {
            let correct = game.res.items().correct_for_drops(game.held(), name);
            batch.text(
                atlas,
                if correct { "✔" } else { "X" },
                icon_x + 18.0,
                row_y + 3.0,
                if correct { 0xFF55FF55 } else { horizon_client::hud::rgba(255, 0, 0, 255) },
                true,
            );
        }
    }
}
