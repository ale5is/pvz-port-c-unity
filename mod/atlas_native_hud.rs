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
    let Some(minimap) = game.native_minimap_now() else {
        return;
    };
    if minimap.corner != horizon_client::extras::Corner::TopRight
        || !game.native_corner_shown(minimap.corner)
    {
        return;
    }

    let clock = game.native_clock();
    let day_time = clock
        .filter(|_| player.dimension == "minecraft:overworld")
        .map(|value| value.day_time);
    let (settings, waypoints, active_set) = crate::game::atlas_mod::with(|state| {
        state.set_day_time(day_time);
        (state.settings(), state.waypoints.clone(), state.set.clone())
    });
    let size = minimap.size;
    let half = size * 0.5;
    let reserved = {
        let effects = &game.potions.effects.list;
        if effects.is_empty() {
            0.0
        } else {
            let harmful = effects.iter().any(|effect| {
                horizon_client::brewing::effect(effect.id)
                    .is_some_and(|info| {
                        info.category != horizon_client::brewing::Category::Beneficial
                    })
            });
            if harmful { 50.0 } else { 25.0 }
        }
    };
    let center = [
        batch.width - 2.0 - half,
        reserved + 2.0 + half,
    ];
    let grid = crate::game::atlas_mod::mini_grid(
        player.x,
        player.z,
        player.yaw,
        minimap.rotate,
        minimap.blocks_per_pixel,
        crate::game::atlas_mod::MINI,
    );

    // Horizon draws the whole 128x128 sprite. Atlas' grid shows 127 texels
    // and carries a fractional shift; crop the sprite by that exact shift so
    // the terrain, the player arrow and world-space markers share one origin.
    let atlas = &game.gui.atlas;
    if let Some(mut image) = atlas.sprites.get(horizon_client::hud::MINIMAP_SPRITE).copied() {
        let du = (image.u1 - image.u0) / image.w as f32;
        let dv = (image.v1 - image.v0) / image.h as f32;
        image.u0 += grid.shift[0] * du;
        image.v0 += grid.shift[1] * dv;
        image.u1 = image.u0 + grid.shown() * du;
        image.v1 = image.v0 + grid.shown() * dv;
        let x = center[0] - half;
        let y = center[1] - half;
        batch.rect_uv(x, y, size, size, image, u32::MAX);

        if let Some(arrow) = atlas.sprites.get("map/decorations/player").copied() {
            let angle = if minimap.rotate { 0.0 } else { 180.0 + player.yaw };
            let (s, c) = (angle + 180.0).to_radians().sin_cos();
            let p = |dx: f32, dy: f32| [
                center[0] + dx * c - dy * s,
                center[1] + dx * s + dy * c,
            ];
            let h = 4.0;
            batch.quad(
                [p(-h, h), p(h, h), p(h, -h), p(-h, -h)],
                [
                    [arrow.u0, arrow.v0],
                    [arrow.u1, arrow.v0],
                    [arrow.u1, arrow.v1],
                    [arrow.u0, arrow.v1],
                ],
                u32::MAX,
            );
        }
    }
    let show_radar = settings.dots
        && (settings.show_players
            || settings.show_mobs
            || settings.show_items
            || settings.show_others);
    if !settings.time && !show_radar && !settings.on_map {
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

    if show_radar {
        // Use the same world-space grid as the terrain texture.
        let reach = 64.0
            * f64::from(minimap.blocks_per_pixel)
            * std::f64::consts::SQRT_2;
        let limit = settings.entity_limit.clamp(1, 1000) as usize;
        let mut entities: Vec<(f64, [f64; 3], u8, Option<String>)> = Vec::new();

        for mob in &game.mobs {
            if mob.removed
                || (settings.hide_invisible
                    && (mob.invisible || mob.has_effect("minecraft:invisibility")))
            {
                continue;
            }
            let dx = mob.position.x - player.x;
            let dz = mob.position.z - player.z;
            if dx.abs() > reach || dz.abs() > reach {
                continue;
            }
            let is_player = mob.kind.id == "minecraft:player"
                || game.remote_player_uuid(mob.id).is_some();
            let dy = mob.position.y - player.y;
            if is_player
                && dx.abs() < 0.05
                && dz.abs() < 0.05
                && dy.abs() < 0.1
            {
                continue;
            }
            let kind = if is_player {
                if !settings.show_players { continue; }
                0
            } else if mob.kind.category == "misc" {
                if !settings.show_others { continue; }
                3
            } else {
                if !settings.show_mobs { continue; }
                1
            };
            if settings.height_limit > 0
                && dy.abs() > f64::from(settings.height_limit)
            {
                continue;
            }
            let name = game
                .remote_player_name(mob.id)
                .map(str::to_owned)
                .or_else(|| Some(mob.kind.id.clone()));
            entities.push((
                dx * dx + dz * dz,
                [mob.position.x, mob.position.y, mob.position.z],
                kind,
                name,
            ));
        }

        if settings.show_items {
            for item in &game.dropped {
                let dx = item.position.x - player.x;
                let dz = item.position.z - player.z;
                if dx.abs() > reach || dz.abs() > reach {
                    continue;
                }
                let dy = item.position.y - player.y;
                if settings.height_limit > 0
                    && dy.abs() > f64::from(settings.height_limit)
                {
                    continue;
                }
                entities.push((
                    dx * dx + dz * dz,
                    [item.position.x, item.position.y, item.position.z],
                    2,
                    Some(item.stack.item.to_string()),
                ));
            }
        }

        if settings.show_others {
            for other in &game.others {
                let dx = other.position.x - player.x;
                let dz = other.position.z - player.z;
                if dx.abs() > reach || dz.abs() > reach {
                    continue;
                }
                let dy = other.position.y - player.y;
                if settings.height_limit > 0
                    && dy.abs() > f64::from(settings.height_limit)
                {
                    continue;
                }
                entities.push((
                    dx * dx + dz * dz,
                    [other.position.x, other.position.y, other.position.z],
                    3,
                    Some(other.kind.clone()),
                ));
            }
        }

        entities.sort_by(|a, b| a.0.total_cmp(&b.0));
        entities.truncate(limit);

        let half = size * 0.5;
        let dot_radius = (size / 100.0).clamp(0.5, 2.0)
            * settings.dot_size.clamp(1, 6) as f32
            * 0.5;
        for (_, position, kind, name) in entities {
            let dx = position[0] - player.x;
            let dz = position[2] - player.z;
            let [screen_x, screen_y] = grid.to_screen(dx, dz, size);
            let px = center[0] + screen_x;
            let py = center[1] + screen_y;
            let local_x = px - center[0];
            let local_y = py - center[1];
            let inside = if minimap.round {
                local_x * local_x + local_y * local_y
                    <= (half - 3.0).powi(2)
            } else {
                local_x.abs() <= half - 3.0 && local_y.abs() <= half - 3.0
            };
            if !inside {
                continue;
            }

            let dy = position[1] - player.y;
            let near = if settings.depth_fade {
                crate::game::atlas_mod::height_fade(dy, settings.fade_from as f32)
            } else {
                1.0
            };
            let light = 0.35 + 0.65 * near;
            let alpha = (0.3 + 0.7 * near) * 255.0;
            let color = match kind {
                0 => 0xFFFFFFFF,
                1 => 0xFFFFF050,
                2 => 0xFFFF2020,
                _ => 0xFFB860FF,
            };
            if kind == 2 {
                let r = dot_radius + 0.7;
                game.native_fill(batch, [px - r, py - r, r * 2.0, r * 2.0], 0xFF101010);
                let r = dot_radius;
                game.native_fill(
                    batch,
                    [px - r, py - r, r * 2.0, r * 2.0],
                    crate::game::atlas_mod::fade(color, light, near),
                );
            } else {
                let radius = if kind == 0 { dot_radius * 1.3 } else { dot_radius };
                let mark = crate::game::atlas_mod::fade(color, light, near);
                let outer = radius + 0.7;
                game.native_fill(batch, [px - 1.0, py - outer, 2.0, outer * 2.0], 0xFF101010);
                game.native_fill(batch, [px - outer, py - 1.0, outer * 2.0, 2.0], 0xFF101010);
                game.native_fill(batch, [px - 0.7, py - radius, 1.4, radius * 2.0], mark);
                game.native_fill(batch, [px - radius, py - 0.7, radius * 2.0, 1.4], mark);
            }

            let show_name = settings.names == 2 || (settings.names == 1 && kind == 0);
            if show_name {
                if let Some(name) = name.as_deref() {
                    game.native_text(
                        batch,
                        name,
                        [px + 3.0, py + 2.0],
                        ((alpha as u32) << 24) | 0x00FFFFFF,
                        true,
                    );
                }
            }
            if settings.entity_height {
                let label = format!("{:+}", dy.round() as i32);
                game.native_text(
                    batch,
                    &label,
                    [px + 3.0, py - 7.0],
                    ((alpha as u32) << 24) | 0x00FFFFFF,
                    true,
                );
            }
        }
    }

    if settings.on_map {
        for waypoint in waypoints.iter().filter(|waypoint| {
            !waypoint.hidden
                && waypoint.dimension == player.dimension
                && (settings.render_all_sets || waypoint.set == active_set)
        }) {
            let dx = waypoint.pos[0] as f64 + 0.5 - player.x;
            let dz = waypoint.pos[2] as f64 + 0.5 - player.z;
            let [screen_x, screen_y] = grid.to_screen(dx, dz, size);
            let px = center[0] + screen_x;
            let py = center[1] + screen_y;
            let lx = px - center[0];
            let ly = py - center[1];
            let inside = if minimap.round {
                lx * lx + ly * ly <= (half - 3.0).powi(2)
            } else {
                lx.abs() <= half - 3.0 && ly.abs() <= half - 3.0
            };
            if !inside {
                continue;
            }
            let color = 0xFF000000 | (waypoint.color & 0x00FFFFFF);
            let radius = 3.0;
            game.native_fill(batch, [px - radius - 1.0, py - 1.0, (radius + 1.0) * 2.0, 2.0], 0xFF101010);
            game.native_fill(batch, [px - 1.0, py - radius - 1.0, 2.0, (radius + 1.0) * 2.0], 0xFF101010);
            game.native_fill(batch, [px - radius, py - 1.0, radius * 2.0, 2.0], color);
            game.native_fill(batch, [px - 1.0, py - radius, 2.0, radius * 2.0], color);
            game.native_text(
                batch,
                &waypoint.name,
                [px + 4.0, py - 4.0],
                0xFFFFFFFF,
                true,
            );
        }
    }

    if settings.time {
        let (x, y) = corners.place(minimap.corner, 112.0, 14.0);
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
