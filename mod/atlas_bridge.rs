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

    let player = game.native_player();
    let dimension = player.as_ref().map(|p| p.dimension.to_owned());
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

    let Some(player) = game.player.as_ref() else {
        m::with(|state| state.save_now_and_then(10.0));
        return;
    };
    let Some(native_player) = player.as_ref().into() else {
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
    m::with(|state| state.save_now_and_then(10.0));
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
                if !m::with(|state| state.wants_scan(cut, key, revision, 1.0)) {
                    continue;
                }

                let top = (0..chunk.sections.len())
                    .rev()
                    .find(|&section| {
                        chunk.sections.get(section).is_some_and(|part| !part.is_uniform(air))
                    })
                    .map_or(chunk.min_y(), |section| {
                        chunk.min_y() + section as i32 * 16 + 15
                    });
                let mut tile = m::Tile::blank(chunk.min_y() as i16);
                for z in 0..16 {
                    for x in 0..16 {
                        let mut y = top;
                        while y > chunk.min_y() {
                            let id = chunk.get(x, y, z) as usize;
                            let Some(block) = facts.states.get(id) else {
                                y -= 1;
                                continue;
                            };
                            if block.map_color == 0 {
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
                            tile.light[index] = game.world.light(wx, y + 1, wz) & 15;
                            break;
                        }
                    }
                }
                m::with(|state| state.put_scan(cut, key, revision, tile));
                if started.elapsed() >= budget {
                    return;
                }
            }
        }
    }
}
