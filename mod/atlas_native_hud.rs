fn atlas_native_hud(
    game: &mut crate::game::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    corners: &mut horizon_client::extras_hud::Corners,
) {
    if !game.native_on("atlas") || !game.native_playing() {
        return;
    }
    let Some(player) = game.native_player() else {
        return;
    };
    let clock = game.native_clock();
    let day_time = clock
        .filter(|_| player.dimension == "minecraft:overworld")
        .map(|value| value.day_time);
    let (settings, enlarged, rotate, blocks_per_pixel) =
        crate::game::atlas_mod::with(|state| {
            state.set_day_time(day_time);
            let rotate = state.turning();
            (
                state.settings(),
                state.enlarged,
                rotate,
                state.mini_zoom_for_render_distance(
                    game.settings.render_distance as i32,
                    rotate,
                ),
            )
        });
    let show_mobs = settings.dots && settings.show_mobs;
    if !settings.time && !show_mobs {
        return;
    }

    let time = day_time.map(crate::game::atlas_mod::time_text);
    let night = day_time.is_some_and(|ticks| {
        let value = ticks.rem_euclid(24_000);
        (13_000..23_000).contains(&value)
    });
    let title = match time {
        Some(value) => format!("{}  {}", if night { "NIGHT" } else { "DAY" }, value),
        None => "ATLAS".to_owned(),
    };

    let corner = horizon_client::extras::Corner::TopRight;
    if !game.native_corner_shown(corner) {
        return;
    }

    if show_mobs {
        let map_size = if enlarged { 160.0 } else { 96.0 };
        // The native minimap has already reserved its corner. Reserving one
        // more box of the same size lets us recover its exact top-left point:
        // the returned y is one map-height plus the HUD gap below the map.
        let (map_x, after_map_y) = corners.place(corner, map_size, map_size);
        let map_y = after_map_y - map_size - 2.0;
        let center = [
            map_x + map_size * 0.5,
            map_y + map_size * 0.5,
        ];
        let axes = crate::game::atlas_mod::mini_axes(rotate, player.yaw);
        let scale = map_size / 128.0 / f64::from(blocks_per_pixel);
        let limit = settings.entity_limit.clamp(1, 128) as usize;
        let mut nearby: Vec<(f64, f32, f32, u32)> = game
            .mobs
            .iter()
            .filter_map(|mob| {
                if mob.removed || mob.kind.id == "minecraft:player" {
                    return None;
                }
                let dx = mob.position.x - player.x;
                let dz = mob.position.z - player.z;
                let right = dx * axes.0[0] + dz * axes.0[1];
                let down = dx * axes.1[0] + dz * axes.1[1];
                let px = (center[0] as f64 + right * scale) as f32;
                let py = (center[1] as f64 + down * scale) as f32;
                let local_x = px - center[0];
                let local_y = py - center[1];
                let inside = if settings.round {
                    local_x * local_x + local_y * local_y
                        <= (map_size * 0.5 - 3.0).powi(2)
                } else {
                    local_x.abs() <= map_size * 0.5 - 3.0
                        && local_y.abs() <= map_size * 0.5 - 3.0
                };
                if !inside {
                    return None;
                }
                let color = if mob.kind.category == "monster" {
                    0xFFFF5555
                } else {
                    0xFFFFFF55
                };
                Some((dx * dx + dz * dz, px, py, color))
            })
            .collect();
        nearby.sort_by(|a, b| a.0.total_cmp(&b.0));
        nearby.truncate(limit);

        game.native_fill(
            batch,
            [center[0] - 2.0, center[1] - 2.0, 4.0, 4.0],
            0xFFFFFFFF,
        );
        for (_, px, py, color) in &nearby {
            game.native_fill(batch, [*px - 2.0, *py - 2.0, 4.0, 4.0], *color);
        }
    }

    if settings.time {
        let (x, y) = corners.place(corner, 112.0, 14.0);
        game.native_fill(batch, [x, y, 112.0, 14.0], 0xA0000000);
        game.native_text(
            batch,
            &title,
            [x + 5.0, y + 3.0],
            if night { 0xFFAA55 } else { 0xFFFFDD },
            true,
        );
    }
}
