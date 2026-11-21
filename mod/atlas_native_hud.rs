fn atlas_native_hud(
    game: &mut crate::game::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    corners: &mut horizon_client::extras_hud::Corners,
) {
    if !game.native_on("atlas") || !game.native_playing() { return; }
    let Some(player) = game.native_player() else { return; };
    let clock = game.native_clock();
    let day_time = clock.filter(|_| player.dimension == "minecraft:overworld").map(|value| value.day_time);
    let (settings, rotate) = crate::game::atlas_mod::with(|state| {
        state.set_day_time(day_time);
        (state.settings(), state.turning())
    });
    if !settings.time && !(settings.dots && settings.show_mobs) { return; }
    let time = day_time.map(crate::game::atlas_mod::time_text);
    let night = day_time.is_some_and(|ticks| {
        let value = ticks.rem_euclid(24_000);
        (13_000..23_000).contains(&value)
    });
    let title = match time {
        Some(value) => format!("ATLAS  {}  {}", if night { "NIGHT" } else { "DAY" }, value),
        None => "ATLAS".to_owned(),
    };
    let radar_enabled = settings.dots && settings.show_mobs;
    let width = 104.0;
    let height = if radar_enabled { 104.0 } else { 18.0 };
    let corner = horizon_client::extras::Corner::TopRight;
    if !game.native_corner_shown(corner) { return; }
    let (x, y) = corners.place(corner, width, height);
    game.native_fill(batch, [x, y, width, height], 0xA0000000);
    game.native_text(batch, &title, [x + 5.0, y + 5.0], if night { 0xFFAA55 } else { 0xFFFFDD }, true);
    if !radar_enabled { return; }
    let map_left = x + 8.0;
    let map_top = y + 22.0;
    let map_size = 88.0;
    let center = [map_left + map_size * 0.5, map_top + map_size * 0.5];
    game.native_fill(batch, [map_left, map_top, map_size, map_size], 0x60000000);
    let axes = crate::game::atlas_mod::mini_axes(rotate, player.yaw);
    let reach = 48.0_f64;
    let radius = 39.0_f64;
    let limit = settings.entity_limit.clamp(1, 128) as usize;
    let mut nearby: Vec<(f64, f32, f32, u32)> = game.mobs.iter().filter_map(|mob| {
        if mob.removed || mob.kind.id == "minecraft:player" { return None; }
        let dx = mob.position.x - player.x;
        let dz = mob.position.z - player.z;
        if dx.abs() > reach || dz.abs() > reach { return None; }
        let right = dx * axes.0[0] + dz * axes.0[1];
        let down = dx * axes.1[0] + dz * axes.1[1];
        let px = (center[0] as f64 + right / reach * radius) as f32;
        let py = (center[1] as f64 + down / reach * radius) as f32;
        let color = if mob.kind.category == "monster" { 0xFFFF5555 } else { 0xFFFFFF55 };
        Some((dx * dx + dz * dz, px, py, color))
    }).collect();
    nearby.sort_by(|a, b| a.0.total_cmp(&b.0));
    nearby.truncate(limit);
    game.native_fill(batch, [center[0] - 2.0, center[1] - 2.0, 4.0, 4.0], 0xFFFFFFFF);
    for (_, px, py, color) in &nearby {
        if *px >= map_left + 2.0 && *px <= map_left + map_size - 4.0 && *py >= map_top + 2.0 && *py <= map_top + map_size - 4.0 {
            game.native_fill(batch, [*px - 2.0, *py - 2.0, 4.0, 4.0], *color);
        }
    }
    let count = format!("MOBS: {}", nearby.len());
    game.native_text(batch, &count, [x + 7.0, y + height - 11.0], 0xFFFFFF, true);
}