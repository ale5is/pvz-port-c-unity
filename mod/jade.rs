// API-80 adaptation of Jade's in-world look-at overlay for Horizon 0.7.8.
// This module deliberately uses only the pinned game's existing APIs.

pub fn register_mod(mods: &mut Vec<crate::game::native_hooks::NativeMod>) {
    if mods.iter().any(|m| m.id == "jade") {
        return;
    }
    mods.push(crate::game::native_hooks::NativeMod {
        id: "jade".into(),
        title: "Jade".into(),
        description: "Shows information about the block you are looking at.".into(),
        keys: Vec::new(),
    });
}

pub fn hud(game: &mut super::Game<'_>, batch: &mut horizon_client::hud::HudBatch) {
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
    }
    .replace('_', " ");
    let detail = format!("{}  {} {} {}", name, hit.pos[0], hit.pos[1], hit.pos[2]);

    let atlas = &game.gui.atlas;
    let title_width = horizon_client::hud::HudBatch::text_width(atlas, &title);
    let detail_width = horizon_client::hud::HudBatch::text_width(atlas, &detail);
    let panel_width = (title_width.max(detail_width) + 12.0).min((batch.width - 16.0).max(0.0));
    let panel_x = ((batch.width - panel_width) * 0.5).max(8.0);
    let panel_y = 8.0;

    batch.fill(atlas, panel_x, panel_y, panel_width, 29.0, 0xD0101010);
    batch.text(atlas, &title, panel_x + 6.0, panel_y + 4.0, 0xFFFFFFFF, true);
    batch.text(atlas, &detail, panel_x + 6.0, panel_y + 16.0, 0xFFB8B8B8, true);
}
