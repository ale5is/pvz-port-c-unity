use crate::game::Game;
use crate::game::atlas_mod as m;
use horizon_client::blocks::{Fluid, MAP_COLORS};
use std::time::{Duration, Instant};

/// API-80 adapter: keep world discovery and chunk scanning independent of
/// the newer Atlas rendering, networking and screen APIs.
pub(super) fn frame(game: &mut Game<'_>) {
    if m::with(|state| state.take_close()) {
        if game.screen == crate::game::Screen::Paused && !game.pause_menu {
            game.screen = crate::game::Screen::Playing;
            game.capture(true);
        }
    }

    let mouse = game.native_cursor();
    m::with(|state| {
        let delta = [mouse[0] - state.mouse[0], mouse[1] - state.mouse[1]];
        if state.map.dragging || state.draw.stroke.is_some() || state.draw.erasing || state.slider.is_some() {
            let button = if state.draw.erasing { 1 } else { 0 };
            state.drag(button, delta, mouse);
        }
    });

    let dir = game.native_data_dir(m::ID);
    let settings = {
        let g = &*game;
        m::with(|s| s.settings_with(|key| g.native_setting(m::ID, key)))
    };
    if let Some(changed) = m::with(|s| s.take_changed_settings()) {
        game.native_set_setting(m::ID, m::SETTINGS_FILE, &changed.to_text());
    }

    let (teleport, say) = m::with(|state| {
        state.can_teleport = game.ui.registries.permission >= 2;
        (state.take_teleport(), state.take_say())
    });
    if let Some(command) = teleport {
        game.send_chat(&command);
    }
    if let Some(message) = say {
        game.send_chat(&message);
    }

    let native_player = game.native_player();
    let dimension = native_player.as_ref().map(|p| p.dimension.to_owned());
    let key = if let Some(world) = game.integrated.as_ref() {
        Some(format!("world-{}", world.world_name))
    } else if let Some(remote) = game.remote.as_ref() {
        Some(m::server_key(settings.server_folder, &remote.session.address, &[]))
    } else {
        None
    };
    m::with(|state| {
        state.load_world(key.clone(), dir.clone());
        let maps = match (&key, &dimension) {
            (Some(world), Some(dimension)) => Some(m::map_dir(&dir, world, dimension)),
            _ => None,
        };
        state.load_map(dimension.clone(), maps);
    });

    let Some(native_player) = native_player else {
        m::with(|state| state.save_now_and_then(10.0));
        return;
    };
    let Some(player) = game.player.as_ref() else {
        m::with(|state| state.save_now_and_then(10.0));
        return;
    };
    let head = (native_player.y + f64::from(player.dimensions.eye_height)).floor() as i32;
    let (bx, bz) = (native_player.x.floor() as i32, native_player.z.floor() as i32);
    let covered = match settings.auto_cave {
        0 => (-1..=1).all(|dx| (-1..=1).all(|dz| game.world.light(bx + dx, head, bz + dz) >> 4 == 0)),
        1 => game.world.light(bx, head, bz) >> 4 == 0,
        _ => false,
    };
    let cut = m::cut_for(
        settings.mode,
        settings.layer_y,
        head,
        covered,
        settings.cave_nether && native_player.dimension == m::NETHER,
    );
    m::with(|state| state.want_cut(cut));
    scan_loaded_chunks(game, cut, settings.cave_depth, native_player.x, native_player.z);
    upload_minimap(game, native_player.x, native_player.z, native_player.yaw);
    if m::with(|state| !matches!(&state.mode, m::Mode::Closed)) {
        upload_map_tiles(game);
    }
    m::with(|state| state.save_now_and_then(10.0));
}

/// Compose Atlas' persistent tile cache into the exact sprite Horizon draws.
/// Horizon's own minimap updater runs before native frame hooks, so this patch
/// is queued afterwards and becomes the image used by draw_minimap.
fn upload_minimap(game: &mut Game<'_>, x: f64, z: f64, yaw: f32) {
    let (size, image) = m::with(|state| {
        state.update_minimap_lit(x, z, yaw, &|x, y, z| game.world.light(x, y, z));
        (state.mini_n, state.mini_image.clone())
    });
    if size == 0 || image.len() != size * size * 4 {
        return;
    }

    let atlas = &game.gui.atlas;
    let Some(rect) = atlas.sprites.get(horizon_client::hud::MINIMAP_SPRITE).copied() else {
        return;
    };
    let (width, height) = rect.texels((atlas.width, atlas.height));
    if width as usize != size || height as usize != size {
        return;
    }

    game.map_patches.push(horizon_client::font::Patch {
        x: (rect.u0 * atlas.width as f32).round() as u32,
        y: (rect.v0 * atlas.height as f32).round() as u32,
        width,
        height,
        rgba: image,
    });
}

/// A chunk is converted into 256 cached columns only when its world revision
/// changes. Zoom and minimap size never trigger this scan.
fn scan_loaded_chunks(
    game: &Game<'_>,
    cut: m::Cut,
    cave_depth: i32,
    player_x: f64,
    player_z: f64,
) {
    let facts = game.res.facts();
    let (cx, cz) = ((player_x.floor() as i32) >> 4, (player_z.floor() as i32) >> 4);
    let radius = game.settings.render_distance.clamp(2, 32);
    let revision = game.world.revision;
    let air = game.world.air.0;
    let cuts = if cut == m::Cut::Surface {
        [Some(m::Cut::Surface), None]
    } else {
        [Some(cut), Some(m::Cut::Surface)]
    };
    let started = Instant::now();
    let budget = Duration::from_micros(2500);

    for ring in 0..=radius {
        for dx in -ring..=ring {
            for dz in -ring..=ring {
                if dx.abs().max(dz.abs()) != ring {
                    continue;
                }
                let key = (cx + dx, cz + dz);
                let Some(chunk) = game.world.chunks.get(&key) else {
                    continue;
                };
                let top = (0..chunk.sections.len())
                    .rev()
                    .find(|&section| {
                        chunk.sections.get(section).is_some_and(|part| !part.is_uniform(air))
                    })
                    .map_or(chunk.min_y(), |section| {
                        chunk.min_y() + section as i32 * 16 + 15
                    });

                for current_cut in cuts.into_iter().flatten() {
                    let interval = if current_cut == cut { 1.0 } else { 5.0 };
                    if !m::with(|state| state.wants_scan(current_cut, key, revision, interval)) {
                        continue;
                    }

                    let mut tile = m::Tile::blank(chunk.min_y() as i16);
                    for z in 0..16 {
                        for x in 0..16 {
                            let mut y = top;
                            let mut wall_color = None;

                            if let m::Cut::Below(cut_y) = current_cut {
                                if cut_y <= top {
                                    let floor = (cut_y - cave_depth.max(1)).max(chunk.min_y());
                                    y = cut_y;
                                    while y > floor {
                                        let id = chunk.get(x, y, z);
                                        let Some(block) = facts.states.get(id as usize) else {
                                            break;
                                        };
                                        let state = horizon_world::registry::BlockStateId(id);
                                        if facts.is_air(state)
                                            || block.fluid != Fluid::None
                                            || block.map_color == 0
                                        {
                                            break;
                                        }
                                        if wall_color.is_none() {
                                            wall_color = Some(
                                                MAP_COLORS
                                                    .get(usize::from(block.map_color))
                                                    .copied()
                                                    .unwrap_or(0)
                                                    & 0x00FF_FFFF,
                                            );
                                        }
                                        y -= 1;
                                    }
                                    if y == floor {
                                        let index = (z * 16 + x) as usize;
                                        tile.color[index] = m::WALL
                                            | m::LAND
                                            | wall_color.unwrap_or(0x0030_3030);
                                        tile.height[index] = cut_y as i16;
                                        continue;
                                    }
                                }
                            }

                            while y > chunk.min_y() {
                                let id = chunk.get(x, y, z);
                                let Some(block) = facts.states.get(id as usize) else {
                                    y -= 1;
                                    continue;
                                };
                                if facts.is_air(horizon_world::registry::BlockStateId(id))
                                    || block.map_color == 0
                                {
                                    y -= 1;
                                    continue;
                                }
                                let color = MAP_COLORS
                                    .get(usize::from(block.map_color))
                                    .copied()
                                    .unwrap_or(0)
                                    & 0x00FF_FFFF;
                                let index = (z * 16 + x) as usize;
                                tile.color[index] = m::LAND | color;
                                tile.height[index] = y as i16;
                                let wx = key.0 * 16 + x;
                                let wz = key.1 * 16 + z;
                                tile.light[index] = game
                                    .world
                                    .light(wx, y + 1, wz)
                                    .max(game.world.light(wx, y, wz))
                                    & 15;
                                break;
                            }
                        }
                    }
                    m::with(|state| state.put_scan(current_cut, key, revision, tile));
                    if started.elapsed() >= budget {
                        return;
                    }
                }
            }
        }
    }
    m::with(|state| state.forget_far_scans((cx, cz), radius + 8));
}


/// Player position shared by the minimap, world map and waypoint controls.
pub(super) fn here(game: &Game<'_>) -> Option<m::Here> {
    let player = game.native_player()?;
    Some(m::Here {
        pos: [player.x, player.y, player.z],
        dimension: player.dimension.to_owned(),
        yaw: player.yaw,
    })
}

/// Claim one of Horizon's map texture slots for a world-map tile.
fn slot_for(game: &mut Game<'_>, id: i32) -> Option<(usize, bool)> {
    if let Some(slot) = game.maps.slots.show(&id) {
        return Some((slot, false));
    }
    let (slot, old) = game.maps.slots.claim(id);
    if let Some(old) = old {
        m::with(|state| state.images.forget_id(old));
    }
    Some((slot, true))
}

/// Upload one 128x128 RGBA world-map tile into a claimed Horizon map slot.
fn upload_map_tile(game: &mut Game<'_>, slot: usize, rgba: Vec<u8>) -> bool {
    let sprite = format!("horizon/map{slot}");
    let Some(rect) = game.gui.atlas.sprites.get(&sprite).copied() else {
        return false;
    };
    game.map_patches.push(horizon_client::font::Patch {
        x: (rect.u0 * game.gui.atlas.width as f32).round() as u32,
        y: (rect.v0 * game.gui.atlas.height as f32).round() as u32,
        width: m::MINI as u32,
        height: m::MINI as u32,
        rgba,
    });
    true
}

/// Build only the visible map tiles, with a small per-frame budget.
fn upload_map_tiles(game: &mut Game<'_>) {
    let tiles = m::with(|state| {
        if state.screen_size[0] <= 0.0 || state.screen_size[1] <= 0.0 {
            return Vec::new();
        }
        state.begin_map_frame();
        state.map.tiles(state.screen_size, state.map_cut(), state.gui_scale)
    });
    if tiles.is_empty() {
        return;
    }

    let started = Instant::now();
    let mut budget = 6usize;
    for (key, _) in tiles {
        let id = m::with(|state| state.images.id(key));
        let Some((slot, fresh)) = slot_for(game, id) else {
            m::with(|state| state.images.slots.remove(&key));
            continue;
        };
        let (stamp, shown) = m::with(|state| {
            (state.tile_stamp(key), state.images.stamps.get(&key).copied())
        });
        if !fresh && shown == Some(stamp) {
            m::with(|state| {
                state.images.slots.insert(key, slot);
            });
            continue;
        }
        if budget == 0 || started.elapsed() > Duration::from_millis(4) {
            if fresh {
                m::with(|state| state.images.slots.remove(&key));
            }
            continue;
        }
        budget -= 1;
        let (image, whole) = m::with(|state| state.compose_tile(key));
        if upload_map_tile(game, slot, image) {
            m::with(|state| {
                if whole {
                    state.images.stamps.insert(key, stamp);
                } else {
                    state.images.stamps.remove(&key);
                }
                state.images.slots.insert(key, slot);
            });
        }
    }
}

/// Draw the portable Atlas UI using Horizon 0.7.8's native HUD primitives.
pub(super) fn paint(
    game: &Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    draws: Vec<m::Draw>,
) {
    for draw in draws {
        match draw {
            m::Draw::Fill(rect, argb) => game.native_fill(batch, rect, argb),
            m::Draw::Text(text, at, rgb, shadow) => {
                game.native_text(batch, &text, at, rgb, shadow);
            }
            m::Draw::Quad(points, argb) => batch.fill_quad(&game.gui.atlas, points, argb),
            m::Draw::Item(item, at, size) => {
                let _ = game.native_item(batch, &item, at, size);
            }
            m::Draw::Skin(skin, dst) => {
                game.native_sprite(batch, &skin, [8.0, 8.0, 8.0, 8.0], dst);
                game.native_sprite(batch, &skin, [40.0, 8.0, 8.0, 8.0], dst);
            }
            m::Draw::Nine(_sprite, rect, argb) => {
                let [x, y, width, height] = rect;
                game.native_fill(batch, rect, argb);
                game.native_fill(batch, [x, y, width, 1.0], 0xFF808080);
                game.native_fill(batch, [x, y + height - 1.0, width, 1.0], 0xFF303030);
                game.native_fill(batch, [x, y, 1.0, height], 0xFF808080);
                game.native_fill(batch, [x + width - 1.0, y, 1.0, height], 0xFF303030);
            }
            m::Draw::Label(text, at, argb, scale, shadow) => {
                let start = batch.vertices.len();
                let [alpha, red, green, blue] = argb.to_be_bytes();
                let color = horizon_client::hud::rgba(red, green, blue, alpha);
                let text = horizon_client::text::Text::literal(text);
                batch.text_styled(&game.gui.atlas, &text, at[0], at[1], color, shadow);
                batch.scale_since(start, at[0], at[1], scale, scale);
            }
            m::Draw::ModImage(src, dst, tint) => {
                paint_map_image(
                    game,
                    batch,
                    horizon_client::hud::MINIMAP_SPRITE,
                    src,
                    dst,
                    tint,
                );
            }
            m::Draw::Image(slot, src, dst, tint) => {
                let sprite = format!("horizon/map{slot}");
                paint_map_image(game, batch, &sprite, src, dst, tint);
            }
        }
    }
}

/// Draw a source rectangle of a map sprite into a GUI rectangle.
fn paint_map_image(
    game: &Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    name: &str,
    src: [f32; 4],
    dst: [f32; 4],
    tint: u32,
) {
    let Some(mut rect) = game.gui.atlas.sprites.get(name).copied() else {
        return;
    };
    let du = (rect.u1 - rect.u0) / rect.w as f32;
    let dv = (rect.v1 - rect.v0) / rect.h as f32;
    rect.u0 += src[0] * du;
    rect.v0 += src[1] * dv;
    rect.u1 = rect.u0 + src[2] * du;
    rect.v1 = rect.v0 + src[3] * dv;
    let [alpha, red, green, blue] = tint.to_be_bytes();
    batch.rect_uv(
        dst[0],
        dst[1],
        dst[2],
        dst[3],
        rect,
        horizon_client::hud::rgba(red, green, blue, alpha),
    );
}
