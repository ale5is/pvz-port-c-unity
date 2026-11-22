use crate::game::Game;
use crate::game::atlas_mod as m;
use horizon_client::blocks::MAP_COLORS;
use std::time::{Duration, Instant};

/// API-80 adapter: keep world discovery and chunk scanning independent of
/// the newer Atlas rendering, networking and screen APIs.
pub(super) fn frame(game: &mut Game<'_>) {
    let dir = game.native_data_dir(m::ID);
    let settings = {
        let g = &*game;
        m::with(|s| s.settings_with(|key| g.native_setting(m::ID, key)))
    };
    if let Some(changed) = m::with(|s| s.take_changed_settings()) {
        game.native_set_setting(m::ID, m::SETTINGS_FILE, &changed.to_text());
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
    let head = (player.position.y + f64::from(player.dimensions.eye_height)).floor() as i32;
    let (bx, bz) = (player.position.x.floor() as i32, player.position.z.floor() as i32);
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
    scan_loaded_chunks(game, cut);
    upload_minimap(game, native_player.yaw);
    m::with(|state| state.save_now_and_then(10.0));
}

/// Compose Atlas' persistent tile cache into the exact sprite Horizon draws.
/// Horizon's own minimap updater runs before native frame hooks, so this patch
/// is queued afterwards and becomes the image used by draw_minimap.
fn upload_minimap(game: &mut Game<'_>, yaw: f32) {
    let Some((x, z)) = game
        .player
        .as_ref()
        .map(|player| (player.position.x, player.position.z))
    else {
        return;
    };

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
fn scan_loaded_chunks(game: &Game<'_>, cut: m::Cut) {
    let Some(player) = game.player.as_ref() else {
        return;
    };
    let facts = game.res.facts();
    let (cx, cz) = (
        (player.position.x.floor() as i32) >> 4,
        (player.position.z.floor() as i32) >> 4,
    );
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
                                    let floor = (cut_y - m::CUT_DEPTH).max(chunk.min_y());
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
