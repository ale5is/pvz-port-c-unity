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
        // A waypoint teleport is a UI action, not a chat message. Send the
        // command through Horizon's command path directly so the player's
        // chat-visibility preference cannot silently discard it.
        game.send_command(command.strip_prefix('/').unwrap_or(&command));
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
                let language = horizon_client::lang::code();
                let localized = atlas_localized_text(&text, &language);
                game.native_text(batch, &localized, at, rgb, shadow);
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


/// Atlas owns its strings rather than changing Minecraft's selected language.
/// Horizon currently bundles these language families; regional Spanish codes
/// deliberately share the Spanish translation table.
pub(super) fn atlas_localized_text(text: &str, language: &str) -> String {
    const ES: &[(&str, &str)] = &[
        ("ATLAS", "ATLAS"), ("DAY", "DÍA"), ("NIGHT", "NOCHE"), ("WORLD MAP", "MAPA DEL MUNDO"), ("Mode", "Modo"), ("Light", "Luz"), ("Radar", "Radar"), ("Grid", "Cuadrícula"), ("Info", "Info"), ("WP", "PR"), ("Me", "Yo"), ("Find", "Buscar"), ("Follow", "Seguir"), ("Undo", "Deshacer"),
        ("Dim", "Dimensión"), ("Draw", "Dibujar"), ("Waypoints", "Puntos de ruta"),
        ("Settings", "Ajustes"), ("Scale", "Escala"), ("Search", "Buscar"),
        ("Search...", "Buscar..."), ("Minimap", "Minimapa"), ("Position", "Posición"),
        ("Size", "Tamaño"), ("Shape", "Forma"), ("Rotate With Player", "Girar con jugador"),
        ("Blocks per Pixel", "Bloques por píxel"), ("Lighting", "Iluminación"),
        ("Cave Mode", "Modo cueva"), ("Layer Mode Height", "Altura de capa"),
        ("Block Color Mode", "Color de bloques"), ("Display Radar", "Mostrar radar"),
        ("Players", "Jugadores"), ("Mobs", "Criaturas"), ("Dropped Items", "Objetos"),
        ("Other Entities", "Otras entidades"), ("Coordinates", "Coordenadas"),
        ("Biome", "Bioma"), ("Time of Day", "Hora del día"), ("Facing", "Orientación"),
        ("Dimension", "Dimensión"), ("Light Level", "Nivel de luz"),
        ("Share Map with Players", "Compartir mapa"), ("Use the Server's Map", "Usar mapa del servidor"),
        ("On-Map Waypoints", "Puntos de ruta en el mapa"), ("In-World Waypoints", "Puntos de ruta en el mundo"),
        ("Compass", "Brújula"), ("Name", "Nombre"), ("Colour", "Color"),
        ("Use my position", "Usar mi posición"), ("Visible", "Visible"),
        ("Save", "Guardar"), ("Cancel", "Cancelar"), ("Delete", "Eliminar"),
        ("Teleport (T)", "Teletransportar (T)"), ("Teleport requires operator permission", "El servidor requiere permisos de operador para teletransportar"), ("Cross-dimensional teleport is disabled in settings", "Activa el teletransporte entre dimensiones en los ajustes"), ("Add/Edit", "Añadir/Editar"),
        ("Share", "Compartir"), ("Enable", "Activar"), ("Disable", "Desactivar"),
        ("Done", "Listo"), ("Filter...", "Filtrar..."), ("Options", "Opciones"),
        ("Clear Set", "Vaciar grupo"), ("Enable All", "Activar todo"), ("Disable All", "Desactivar todo"),
        ("Back", "Atrás"), ("Prev", "Anterior"), ("Next", "Siguiente"),
        ("World/Server", "Mundo/Servidor"), ("Sub-World/Dimension", "Submundo/Dimensión"),
        ("Edit waypoint", "Editar punto de ruta"), ("New waypoint", "Nuevo punto de ruta"),
        ("No waypoints here. Press B in the game or on the world map.", "No hay puntos de ruta. Pulsa B en el juego o en el mapa."),
        ("other dimension", "otra dimensión"), ("deathpoint", "punto de muerte"),
        ("temporary", "temporal"), ("disabled", "desactivado"),
        ("Use my position", "Usar mi posición"), ("Visible: No", "Visible: No"),
        ("Visible: Yes", "Visible: Sí"), ("Enter save  -  Tab next field  -  Esc cancel", "Enter guardar  -  Tab siguiente campo  -  Esc cancelar"),
        ("No waypoints here", "No hay puntos de ruta"), ("Click to edit", "Pulsa para editar"),
        ("Drag to draw, right drag to erase, Z to undo, D to stop drawing", "Arrastra para dibujar, clic derecho para borrar, Z deshacer, D terminar"),
        ("Drag to move, tap +/- or [ / ] to zoom, B for a waypoint here, Space to centre, D to draw", "Arrastra para mover, +/- zoom, B crear punto, Espacio centrar, D dibujar"),
    ];
    const ZH: &[(&str, &str)] = &[
        ("ATLAS", "ATLAS"), ("DAY", "白天"), ("NIGHT", "夜晚"), ("WORLD MAP", "世界地图"), ("Mode", "模式"), ("Dim", "维度"), ("Light", "光照"), ("Radar", "雷达"), ("Grid", "网格"), ("Info", "信息"), ("WP", "路径点"), ("Me", "我"), ("Find", "查找"), ("Follow", "跟随"), ("Undo", "撤销"),
        ("Draw", "绘制"), ("Waypoints", "路径点"), ("Settings", "设置"), ("Scale", "比例"),
        ("Search", "搜索"), ("Search...", "搜索..."), ("Minimap", "小地图"), ("Position", "位置"),
        ("Size", "大小"), ("Shape", "形状"), ("Rotate With Player", "随玩家旋转"),
        ("Blocks per Pixel", "每像素方块数"), ("Lighting", "光照"), ("Cave Mode", "洞穴模式"),
        ("Layer Mode Height", "层模式高度"), ("Block Color Mode", "方块颜色模式"),
        ("Display Radar", "显示雷达"), ("Players", "玩家"), ("Mobs", "生物"),
        ("Dropped Items", "掉落物"), ("Other Entities", "其他实体"), ("Coordinates", "坐标"),
        ("Biome", "生物群系"), ("Time of Day", "时间"), ("Facing", "朝向"),
        ("Dimension", "维度"), ("Light Level", "亮度"), ("Share Map with Players", "与玩家共享地图"),
        ("Use the Server's Map", "使用服务器地图"), ("On-Map Waypoints", "地图路径点"),
        ("In-World Waypoints", "世界路径点"), ("Compass", "指南针"), ("Name", "名称"),
        ("Colour", "颜色"), ("Use my position", "使用我的位置"), ("Visible", "可见"),
        ("Save", "保存"), ("Cancel", "取消"), ("Delete", "删除"), ("Teleport (T)", "传送 (T)"), ("Teleport requires operator permission", "需要服务器管理员权限才能传送"), ("Cross-dimensional teleport is disabled in settings", "请在设置中启用跨维度传送"),
        ("Add/Edit", "添加/编辑"), ("Share", "分享"), ("Enable", "启用"), ("Disable", "禁用"),
        ("Done", "完成"), ("Filter...", "筛选..."), ("Options", "选项"), ("Clear Set", "清空组"),
        ("Enable All", "全部启用"), ("Disable All", "全部禁用"), ("Back", "返回"),
        ("Prev", "上一页"), ("Next", "下一页"), ("World/Server", "世界/服务器"),
        ("Sub-World/Dimension", "子世界/维度"), ("Edit waypoint", "编辑路径点"),
        ("New waypoint", "新建路径点"), ("other dimension", "其他维度"), ("deathpoint", "死亡点"),
        ("temporary", "临时"), ("disabled", "已禁用"), ("Visible: No", "可见：否"),
        ("Visible: Yes", "可见：是"), ("Click to edit", "点击编辑"),
        ("No waypoints here. Press B in the game or on the world map.", "此处没有路径点。在游戏或世界地图中按 B 创建。"),
        ("Drag to draw, right drag to erase, Z to undo, D to stop drawing", "拖动绘制，右键拖动擦除，Z 撤销，D 停止绘制"),
        ("Drag to move, tap +/- or [ / ] to zoom, B for a waypoint here, Space to centre, D to draw", "拖动移动，+/- 缩放，B 创建路径点，空格居中，D 绘制"),
    ];
    const RU: &[(&str, &str)] = &[
        ("ATLAS", "ATLAS"), ("DAY", "ДЕНЬ"), ("NIGHT", "НОЧЬ"), ("WORLD MAP", "КАРТА МИРА"), ("Mode", "Режим"),
        ("Dim", "Измерение"), ("Light", "Свет"), ("Radar", "Радар"), ("Grid", "Сетка"), ("Info", "Инфо"), ("WP", "Метки"), ("Me", "Я"), ("Find", "Найти"), ("Follow", "Следовать"), ("Undo", "Отмена"), ("Draw", "Рисование"), ("Waypoints", "Метки"),
        ("Settings", "Настройки"), ("Scale", "Масштаб"), ("Search", "Поиск"),
        ("Search...", "Поиск..."), ("Minimap", "Мини-карта"), ("Position", "Положение"),
        ("Size", "Размер"), ("Shape", "Форма"), ("Rotate With Player", "Поворот за игроком"),
        ("Blocks per Pixel", "Блоков на пиксель"), ("Lighting", "Освещение"),
        ("Cave Mode", "Режим пещер"), ("Layer Mode Height", "Высота слоя"),
        ("Block Color Mode", "Цвет блоков"), ("Display Radar", "Показывать радар"),
        ("Players", "Игроки"), ("Mobs", "Мобы"), ("Dropped Items", "Предметы"),
        ("Other Entities", "Другие сущности"), ("Coordinates", "Координаты"),
        ("Biome", "Биом"), ("Time of Day", "Время суток"), ("Facing", "Направление"),
        ("Dimension", "Измерение"), ("Light Level", "Уровень света"),
        ("Share Map with Players", "Делиться картой"), ("Use the Server's Map", "Карта сервера"),
        ("On-Map Waypoints", "Метки на карте"), ("In-World Waypoints", "Метки в мире"),
        ("Compass", "Компас"), ("Name", "Название"), ("Colour", "Цвет"),
        ("Use my position", "Моя позиция"), ("Visible", "Видимость"), ("Save", "Сохранить"),
        ("Cancel", "Отмена"), ("Delete", "Удалить"), ("Teleport (T)", "Телепорт (T)"), ("Teleport requires operator permission", "Для телепортации нужны права оператора"), ("Cross-dimensional teleport is disabled in settings", "Включите телепортацию между измерениями в настройках"),
        ("Add/Edit", "Добавить/изменить"), ("Share", "Поделиться"), ("Enable", "Включить"),
        ("Disable", "Выключить"), ("Done", "Готово"), ("Filter...", "Фильтр..."),
        ("Options", "Параметры"), ("Clear Set", "Очистить набор"), ("Enable All", "Включить все"),
        ("Disable All", "Выключить все"), ("Back", "Назад"), ("Prev", "Назад"), ("Next", "Далее"),
        ("World/Server", "Мир/Сервер"), ("Sub-World/Dimension", "Подмир/измерение"),
        ("Edit waypoint", "Изменить метку"), ("New waypoint", "Новая метка"),
        ("other dimension", "другое измерение"), ("deathpoint", "точка смерти"),
        ("temporary", "временная"), ("disabled", "отключено"), ("Visible: No", "Видимость: нет"),
        ("Visible: Yes", "Видимость: да"), ("Click to edit", "Нажмите для изменения"),
        ("No waypoints here. Press B in the game or on the world map.", "Здесь нет меток. Нажмите B в игре или на карте."),
        ("Drag to draw, right drag to erase, Z to undo, D to stop drawing", "Перетаскивайте для рисования, правая кнопка — стереть, Z — отмена, D — стоп"),
        ("Drag to move, tap +/- or [ / ] to zoom, B for a waypoint here, Space to centre, D to draw", "Перетаскивайте карту, +/- масштаб, B — метка, пробел — центр, D — рисование"),
    ];
    const PL: &[(&str, &str)] = &[
        ("ATLAS", "ATLAS"), ("DAY", "DZIEŃ"), ("NIGHT", "NOC"), ("WORLD MAP", "MAPA ŚWIATA"), ("Mode", "Tryb"), ("Dim", "Wymiar"), ("Light", "Światło"), ("Radar", "Radar"), ("Grid", "Siatka"), ("Info", "Info"), ("WP", "Punkty"), ("Me", "Ja"), ("Find", "Szukaj"), ("Follow", "Podążaj"), ("Undo", "Cofnij"),
        ("Draw", "Rysuj"), ("Waypoints", "Punkty trasy"), ("Settings", "Ustawienia"),
        ("Scale", "Skala"), ("Search", "Szukaj"), ("Search...", "Szukaj..."), ("Minimap", "Minimapa"),
        ("Position", "Pozycja"), ("Size", "Rozmiar"), ("Shape", "Kształt"),
        ("Rotate With Player", "Obracaj z graczem"), ("Blocks per Pixel", "Bloków na piksel"),
        ("Lighting", "Oświetlenie"), ("Cave Mode", "Tryb jaskiń"), ("Layer Mode Height", "Wysokość warstwy"),
        ("Block Color Mode", "Kolor bloków"), ("Display Radar", "Pokaż radar"), ("Players", "Gracze"),
        ("Mobs", "Moby"), ("Dropped Items", "Upuszczone przedmioty"), ("Other Entities", "Inne byty"),
        ("Coordinates", "Współrzędne"), ("Biome", "Biom"), ("Time of Day", "Pora dnia"),
        ("Facing", "Kierunek"), ("Dimension", "Wymiar"), ("Light Level", "Poziom światła"),
        ("Share Map with Players", "Udostępniaj mapę"), ("Use the Server's Map", "Użyj mapy serwera"),
        ("On-Map Waypoints", "Punkty na mapie"), ("In-World Waypoints", "Punkty w świecie"),
        ("Compass", "Kompas"), ("Name", "Nazwa"), ("Colour", "Kolor"), ("Use my position", "Użyj mojej pozycji"),
        ("Visible", "Widoczność"), ("Save", "Zapisz"), ("Cancel", "Anuluj"), ("Delete", "Usuń"),
        ("Teleport (T)", "Teleportuj (T)"), ("Teleport requires operator permission", "Teleport wymaga uprawnień operatora"), ("Cross-dimensional teleport is disabled in settings", "Włącz teleportację między wymiarami w ustawieniach"), ("Add/Edit", "Dodaj/Edytuj"), ("Share", "Udostępnij"),
        ("Enable", "Włącz"), ("Disable", "Wyłącz"), ("Done", "Gotowe"), ("Filter...", "Filtruj..."),
        ("Options", "Opcje"), ("Clear Set", "Wyczyść zestaw"), ("Enable All", "Włącz wszystko"),
        ("Disable All", "Wyłącz wszystko"), ("Back", "Wstecz"), ("Prev", "Poprzednia"),
        ("Next", "Następna"), ("World/Server", "Świat/Serwer"), ("Sub-World/Dimension", "Podświat/Wymiar"),
        ("Edit waypoint", "Edytuj punkt"), ("New waypoint", "Nowy punkt"), ("other dimension", "inny wymiar"),
        ("deathpoint", "punkt śmierci"), ("temporary", "tymczasowy"), ("disabled", "wyłączony"),
        ("Visible: No", "Widoczność: nie"), ("Visible: Yes", "Widoczność: tak"),
        ("Click to edit", "Kliknij, aby edytować"),
        ("No waypoints here. Press B in the game or on the world map.", "Brak punktów. Naciśnij B w grze lub na mapie."),
        ("Drag to draw, right drag to erase, Z to undo, D to stop drawing", "Przeciągnij, aby rysować; prawy przycisk usuwa; Z cofa; D kończy"),
        ("Drag to move, tap +/- or [ / ] to zoom, B for a waypoint here, Space to centre, D to draw", "Przeciągnij mapę, +/- powiększa, B dodaje punkt, spacja centruje, D rysuje"),
    ];
    const UK: &[(&str, &str)] = &[
        ("ATLAS", "ATLAS"), ("DAY", "ДЕНЬ"), ("NIGHT", "НІЧ"), ("WORLD MAP", "МАПА СВІТУ"), ("Mode", "Режим"),
        ("Dim", "Вимір"), ("Light", "Світло"), ("Radar", "Радар"), ("Grid", "Сітка"), ("Info", "Інфо"), ("WP", "Точки"), ("Me", "Я"), ("Find", "Знайти"), ("Follow", "Слідувати"), ("Undo", "Скасувати"), ("Draw", "Малювання"), ("Waypoints", "Точки маршруту"),
        ("Settings", "Налаштування"), ("Scale", "Масштаб"), ("Search", "Пошук"),
        ("Search...", "Пошук..."), ("Minimap", "Мінімапа"), ("Position", "Позиція"),
        ("Size", "Розмір"), ("Shape", "Форма"), ("Rotate With Player", "Обертати з гравцем"),
        ("Blocks per Pixel", "Блоків на піксель"), ("Lighting", "Освітлення"),
        ("Cave Mode", "Режим печер"), ("Layer Mode Height", "Висота шару"),
        ("Block Color Mode", "Колір блоків"), ("Display Radar", "Показувати радар"),
        ("Players", "Гравці"), ("Mobs", "Моби"), ("Dropped Items", "Викинуті предмети"),
        ("Other Entities", "Інші сутності"), ("Coordinates", "Координати"), ("Biome", "Біом"),
        ("Time of Day", "Час доби"), ("Facing", "Напрямок"), ("Dimension", "Вимір"),
        ("Light Level", "Рівень світла"), ("Share Map with Players", "Ділитися мапою"),
        ("Use the Server's Map", "Мапа сервера"), ("On-Map Waypoints", "Точки на мапі"),
        ("In-World Waypoints", "Точки у світі"), ("Compass", "Компас"), ("Name", "Назва"),
        ("Colour", "Колір"), ("Use my position", "Моя позиція"), ("Visible", "Видимість"),
        ("Save", "Зберегти"), ("Cancel", "Скасувати"), ("Delete", "Видалити"),
        ("Teleport (T)", "Телепортувати (T)"), ("Teleport requires operator permission", "Для телепортації потрібні права оператора"), ("Cross-dimensional teleport is disabled in settings", "Увімкніть телепортацію між вимірами в налаштуваннях"), ("Add/Edit", "Додати/Змінити"),
        ("Share", "Поділитися"), ("Enable", "Увімкнути"), ("Disable", "Вимкнути"),
        ("Done", "Готово"), ("Filter...", "Фільтр..."), ("Options", "Параметри"),
        ("Clear Set", "Очистити набір"), ("Enable All", "Увімкнути все"), ("Disable All", "Вимкнути все"),
        ("Back", "Назад"), ("Prev", "Попередня"), ("Next", "Наступна"),
        ("World/Server", "Світ/Сервер"), ("Sub-World/Dimension", "Підсвіт/Вимір"),
        ("Edit waypoint", "Редагувати точку"), ("New waypoint", "Нова точка"),
        ("other dimension", "інший вимір"), ("deathpoint", "точка смерті"),
        ("temporary", "тимчасова"), ("disabled", "вимкнено"), ("Visible: No", "Видимість: ні"),
        ("Visible: Yes", "Видимість: так"), ("Click to edit", "Натисніть для редагування"),
        ("No waypoints here. Press B in the game or on the world map.", "Тут немає точок. Натисніть B у грі або на мапі."),
        ("Drag to draw, right drag to erase, Z to undo, D to stop drawing", "Перетягуйте для малювання, права кнопка стирає, Z скасовує, D завершує"),
        ("Drag to move, tap +/- or [ / ] to zoom, B for a waypoint here, Space to centre, D to draw", "Перетягуйте мапу, +/- масштаб, B — точка, пробіл — центр, D — малювання"),
    ];
    const TR: &[(&str, &str)] = &[
        ("ATLAS", "ATLAS"), ("DAY", "GÜNDÜZ"), ("NIGHT", "GECE"), ("WORLD MAP", "DÜNYA HARİTASI"), ("Mode", "Mod"),
        ("Dim", "Boyut"), ("Light", "Işık"), ("Radar", "Radar"), ("Grid", "Izgara"), ("Info", "Bilgi"), ("WP", "İşaret"), ("Me", "Ben"), ("Find", "Bul"), ("Follow", "Takip"), ("Undo", "Geri al"), ("Draw", "Çiz"), ("Waypoints", "İşaretçiler"),
        ("Settings", "Ayarlar"), ("Scale", "Ölçek"), ("Search", "Ara"), ("Search...", "Ara..."),
        ("Minimap", "Mini harita"), ("Position", "Konum"), ("Size", "Boyut"), ("Shape", "Şekil"),
        ("Rotate With Player", "Oyuncuyla döndür"), ("Blocks per Pixel", "Piksel başına blok"),
        ("Lighting", "Aydınlatma"), ("Cave Mode", "Mağara modu"), ("Layer Mode Height", "Katman yüksekliği"),
        ("Block Color Mode", "Blok renk modu"), ("Display Radar", "Radarı göster"), ("Players", "Oyuncular"),
        ("Mobs", "Yaratıklar"), ("Dropped Items", "Düşen eşyalar"), ("Other Entities", "Diğer varlıklar"),
        ("Coordinates", "Koordinatlar"), ("Biome", "Biyom"), ("Time of Day", "Günün saati"),
        ("Facing", "Yön"), ("Dimension", "Boyut"), ("Light Level", "Işık seviyesi"),
        ("Share Map with Players", "Haritayı paylaş"), ("Use the Server's Map", "Sunucu haritasını kullan"),
        ("On-Map Waypoints", "Haritadaki işaretçiler"), ("In-World Waypoints", "Dünyadaki işaretçiler"),
        ("Compass", "Pusula"), ("Name", "Ad"), ("Colour", "Renk"), ("Use my position", "Konumumu kullan"),
        ("Visible", "Görünür"), ("Save", "Kaydet"), ("Cancel", "İptal"), ("Delete", "Sil"),
        ("Teleport (T)", "Işınlan (T)"), ("Teleport requires operator permission", "Işınlanmak için operatör izni gerekir"), ("Cross-dimensional teleport is disabled in settings", "Ayarlardan boyutlar arası ışınlanmayı etkinleştirin"), ("Add/Edit", "Ekle/Düzenle"), ("Share", "Paylaş"),
        ("Enable", "Etkinleştir"), ("Disable", "Devre dışı bırak"), ("Done", "Bitti"),
        ("Filter...", "Filtrele..."), ("Options", "Seçenekler"), ("Clear Set", "Grubu temizle"),
        ("Enable All", "Tümünü etkinleştir"), ("Disable All", "Tümünü devre dışı bırak"),
        ("Back", "Geri"), ("Prev", "Önceki"), ("Next", "Sonraki"), ("World/Server", "Dünya/Sunucu"),
        ("Sub-World/Dimension", "Alt dünya/Boyut"), ("Edit waypoint", "İşaretçiyi düzenle"),
        ("New waypoint", "Yeni işaretçi"), ("other dimension", "diğer boyut"), ("deathpoint", "ölüm noktası"),
        ("temporary", "geçici"), ("disabled", "devre dışı"), ("Visible: No", "Görünür: Hayır"),
        ("Visible: Yes", "Görünür: Evet"), ("Click to edit", "Düzenlemek için tıkla"),
        ("No waypoints here. Press B in the game or on the world map.", "Burada işaretçi yok. Oyunda veya haritada B'ye bas."),
        ("Drag to draw, right drag to erase, Z to undo, D to stop drawing", "Çizmek için sürükle, silmek için sağ sürükle, Z geri alır, D bitirir"),
        ("Drag to move, tap +/- or [ / ] to zoom, B for a waypoint here, Space to centre, D to draw", "Haritayı sürükle, +/- yakınlaştır, B işaretçi ekler, Space ortalar, D çizer"),
    ];

    let language = if language.starts_with("es_") { "es_es" } else { language };
    const ES_EXTRA: &[(&str, &str)] = &[
        ("Minimap Settings", "Ajustes del minimapa"), ("View Settings", "Ajustes de vista"),
        ("Block Map Settings", "Ajustes del mapa de bloques"), ("Entity Radar Settings", "Ajustes del radar"),
        ("Overlay Settings", "Ajustes de superposición"), ("Information Settings", "Ajustes de información"),
        ("Waypoint Settings", "Ajustes de puntos de ruta"), ("Miscellaneous Settings", "Otros ajustes"),
        ("Opacity", "Opacidad"), ("Frame Style", "Estilo del marco"), ("Frame Color", "Color del marco"),
        ("Center When Enlarged", "Centrar al ampliar"), ("Hide When Under Screen", "Ocultar bajo pantallas"),
        ("Auto Cave Mode", "Modo cueva automático"), ("Terrain Slopes", "Pendientes del terreno"),
        ("Block Transparency", "Transparencia de bloques"), ("Show Flowers", "Mostrar flores"),
        ("Display Icons", "Mostrar iconos"), ("Display Names", "Mostrar nombres"),
        ("Chunk Grid", "Cuadrícula de chunks"), ("Light Overlay", "Superposición de luz"),
        ("Slime Chunks", "Chunks de slime"), ("Portal Highlights", "Resaltar portales"),
        ("New Chunks", "Chunks nuevos"), ("Old Chunks", "Chunks antiguos"),
        ("Waypoint Beacons", "Balizas de puntos"), ("Cross-Dimensional TP", "Teletransporte entre dimensiones"),
        ("Hide Waypoint Coordinates", "Ocultar coordenadas"), ("Compass Scale", "Escala de brújula"),
    ];
    const ZH_EXTRA: &[(&str, &str)] = &[
        ("Minimap Settings", "小地图设置"), ("View Settings", "视图设置"), ("Block Map Settings", "方块地图设置"),
        ("Entity Radar Settings", "实体雷达设置"), ("Overlay Settings", "叠加层设置"),
        ("Information Settings", "信息设置"), ("Waypoint Settings", "路径点设置"), ("Miscellaneous Settings", "其他设置"),
        ("Opacity", "不透明度"), ("Frame Style", "边框样式"), ("Frame Color", "边框颜色"),
        ("Center When Enlarged", "放大时居中"), ("Hide When Under Screen", "界面打开时隐藏"),
        ("Auto Cave Mode", "自动洞穴模式"), ("Terrain Slopes", "地形坡度"), ("Block Transparency", "方块透明度"),
        ("Show Flowers", "显示花朵"), ("Display Icons", "显示图标"), ("Display Names", "显示名称"),
        ("Chunk Grid", "区块网格"), ("Light Overlay", "光照覆盖"), ("Slime Chunks", "史莱姆区块"),
        ("Portal Highlights", "突出显示传送门"), ("New Chunks", "新区块"), ("Old Chunks", "旧区块"),
        ("Waypoint Beacons", "路径点信标"), ("Cross-Dimensional TP", "跨维度传送"),
        ("Hide Waypoint Coordinates", "隐藏路径点坐标"), ("Compass Scale", "指南针大小"),
    ];
    const RU_EXTRA: &[(&str, &str)] = &[
        ("Minimap Settings", "Настройки мини-карты"), ("View Settings", "Настройки отображения"),
        ("Block Map Settings", "Настройки карты блоков"), ("Entity Radar Settings", "Настройки радара"),
        ("Overlay Settings", "Настройки слоёв"), ("Information Settings", "Настройки информации"),
        ("Waypoint Settings", "Настройки меток"), ("Miscellaneous Settings", "Прочие настройки"),
        ("Opacity", "Непрозрачность"), ("Frame Style", "Стиль рамки"), ("Frame Color", "Цвет рамки"),
        ("Center When Enlarged", "Центрировать при увеличении"), ("Hide When Under Screen", "Скрывать под экранами"),
        ("Auto Cave Mode", "Авто режим пещер"), ("Terrain Slopes", "Уклоны местности"),
        ("Block Transparency", "Прозрачность блоков"), ("Show Flowers", "Показывать цветы"),
        ("Display Icons", "Показывать значки"), ("Display Names", "Показывать имена"),
        ("Chunk Grid", "Сетка чанков"), ("Light Overlay", "Слой освещения"), ("Slime Chunks", "Слайм-чанки"),
        ("Portal Highlights", "Подсветка порталов"), ("New Chunks", "Новые чанки"), ("Old Chunks", "Старые чанки"),
        ("Waypoint Beacons", "Маяки меток"), ("Cross-Dimensional TP", "Телепорт между измерениями"),
        ("Hide Waypoint Coordinates", "Скрывать координаты меток"), ("Compass Scale", "Размер компаса"),
    ];
    const PL_EXTRA: &[(&str, &str)] = &[
        ("Minimap Settings", "Ustawienia minimapy"), ("View Settings", "Ustawienia widoku"),
        ("Block Map Settings", "Ustawienia mapy bloków"), ("Entity Radar Settings", "Ustawienia radaru"),
        ("Overlay Settings", "Ustawienia nakładki"), ("Information Settings", "Ustawienia informacji"),
        ("Waypoint Settings", "Ustawienia punktów"), ("Miscellaneous Settings", "Pozostałe ustawienia"),
        ("Opacity", "Nieprzezroczystość"), ("Frame Style", "Styl ramki"), ("Frame Color", "Kolor ramki"),
        ("Center When Enlarged", "Wyśrodkuj po powiększeniu"), ("Hide When Under Screen", "Ukryj pod ekranami"),
        ("Auto Cave Mode", "Automatyczny tryb jaskiń"), ("Terrain Slopes", "Nachylenie terenu"),
        ("Block Transparency", "Przezroczystość bloków"), ("Show Flowers", "Pokaż kwiaty"),
        ("Display Icons", "Pokaż ikony"), ("Display Names", "Pokaż nazwy"), ("Chunk Grid", "Siatka chunków"),
        ("Light Overlay", "Nakładka światła"), ("Slime Chunks", "Chunki slime"), ("Portal Highlights", "Podświetlenie portali"),
        ("New Chunks", "Nowe chunki"), ("Old Chunks", "Stare chunki"), ("Waypoint Beacons", "Latarnie punktów"),
        ("Cross-Dimensional TP", "Teleportacja między wymiarami"), ("Hide Waypoint Coordinates", "Ukryj współrzędne punktów"),
        ("Compass Scale", "Skala kompasu"),
    ];
    const UK_EXTRA: &[(&str, &str)] = &[
        ("Minimap Settings", "Налаштування мінімапи"), ("View Settings", "Налаштування вигляду"),
        ("Block Map Settings", "Налаштування мапи блоків"), ("Entity Radar Settings", "Налаштування радара"),
        ("Overlay Settings", "Налаштування накладання"), ("Information Settings", "Налаштування інформації"),
        ("Waypoint Settings", "Налаштування точок"), ("Miscellaneous Settings", "Інші налаштування"),
        ("Opacity", "Непрозорість"), ("Frame Style", "Стиль рамки"), ("Frame Color", "Колір рамки"),
        ("Center When Enlarged", "Центрувати при збільшенні"), ("Hide When Under Screen", "Ховати під екранами"),
        ("Auto Cave Mode", "Автоматичний режим печер"), ("Terrain Slopes", "Схили місцевості"),
        ("Block Transparency", "Прозорість блоків"), ("Show Flowers", "Показувати квіти"),
        ("Display Icons", "Показувати значки"), ("Display Names", "Показувати імена"),
        ("Chunk Grid", "Сітка чанків"), ("Light Overlay", "Шар освітлення"), ("Slime Chunks", "Слайм-чанки"),
        ("Portal Highlights", "Підсвічування порталів"), ("New Chunks", "Нові чанки"), ("Old Chunks", "Старі чанки"),
        ("Waypoint Beacons", "Маяки точок"), ("Cross-Dimensional TP", "Телепортація між вимірами"),
        ("Hide Waypoint Coordinates", "Ховати координати точок"), ("Compass Scale", "Розмір компаса"),
    ];
    const TR_EXTRA: &[(&str, &str)] = &[
        ("Minimap Settings", "Mini harita ayarları"), ("View Settings", "Görünüm ayarları"),
        ("Block Map Settings", "Blok haritası ayarları"), ("Entity Radar Settings", "Varlık radarı ayarları"),
        ("Overlay Settings", "Katman ayarları"), ("Information Settings", "Bilgi ayarları"),
        ("Waypoint Settings", "İşaretçi ayarları"), ("Miscellaneous Settings", "Diğer ayarlar"),
        ("Opacity", "Opaklık"), ("Frame Style", "Çerçeve stili"), ("Frame Color", "Çerçeve rengi"),
        ("Center When Enlarged", "Büyütülünce ortala"), ("Hide When Under Screen", "Ekranların altında gizle"),
        ("Auto Cave Mode", "Otomatik mağara modu"), ("Terrain Slopes", "Arazi eğimleri"),
        ("Block Transparency", "Blok saydamlığı"), ("Show Flowers", "Çiçekleri göster"),
        ("Display Icons", "Simgeleri göster"), ("Display Names", "Adları göster"), ("Chunk Grid", "Chunk ızgarası"),
        ("Light Overlay", "Işık katmanı"), ("Slime Chunks", "Slime chunkları"), ("Portal Highlights", "Portalları vurgula"),
        ("New Chunks", "Yeni chunklar"), ("Old Chunks", "Eski chunklar"), ("Waypoint Beacons", "İşaretçi ışınları"),
        ("Cross-Dimensional TP", "Boyutlar arası ışınlanma"), ("Hide Waypoint Coordinates", "İşaretçi koordinatlarını gizle"),
        ("Compass Scale", "Pusula boyutu"),
    ];
    const ES_MORE: &[(&str, &str)] = &[
        ("Cave Mode Zoom In", "Zoom del modo cueva"), ("Zoom Level When Enlarged", "Zoom al ampliar"),
        ("Undiscovered Opacity", "Opacidad de zonas no exploradas"), ("Keep Rotation When Enlarged", "Mantener rotación al ampliar"),
        ("Toggled Enlarged Minimap", "Alternar minimapa ampliado"), ("Minimap FPS Limiter", "Límite de FPS del minimapa"),
        ("Minimap Shows the Map's Dimension", "El minimapa muestra la dimensión del mapa"), ("Minimap FPS Limit", "Límite de FPS del minimapa"),
        ("Cave Mode in the Nether", "Modo cueva en el Nether"), ("Cave Mode Depth", "Profundidad del modo cueva"),
        ("Cave Mode Toggle Timer", "Tiempo para alternar modo cueva"), ("Terrain Depth", "Profundidad del terreno"),
        ("Show Grass and Plants", "Mostrar hierba y plantas"), ("Show Tree Leaves", "Mostrar hojas de árboles"),
        ("Display Stained Glass", "Mostrar cristal tintado"), ("Transparent Obsidian Roof", "Techo de obsidiana transparente"),
        ("Obsidian Roof Y", "Altura Y del techo de obsidiana"), ("Entity Dot Size", "Tamaño del punto de entidad"),
        ("Entity Icon Scale", "Escala del icono de entidad"), ("Entity Number", "Cantidad de entidades"),
        ("Entity Height Limit", "Límite de altura de entidades"), ("Show Entity Depth", "Mostrar profundidad de entidades"),
        ("Min Y Distance For Depth Fade", "Distancia Y mínima para atenuar profundidad"), ("Display Entity Height", "Mostrar altura de entidades"),
        ("Main Entity As", "Entidad principal como"), ("Arrow Scale", "Escala de flecha"), ("Arrow Color", "Color de flecha"),
        ("Arrow Opacity", "Opacidad de flecha"), ("Hide Invisible Entities", "Ocultar entidades invisibles"),
        ("Chunk Grid Line Width", "Grosor de líneas de cuadrícula"), ("Light Overlay Below Level", "Superponer luz bajo nivel"),
        ("Server View Distance", "Distancia de visión del servidor"), ("Chunk", "Chunk"), ("Weather", "Clima"),
        ("Info Display Background Opacity", "Opacidad del fondo informativo"), ("Info Text Alignment", "Alineación del texto informativo"),
        ("Map Sharing Rate", "Velocidad de compartir mapa"), ("Server Map Folder", "Carpeta del mapa del servidor"),
        ("UI Scale", "Escala de interfaz"), ("In-World Waypoint Icon Scale", "Escala del icono de punto en el mundo"),
        ("Waypoint Name Text Scale", "Escala del texto del nombre"), ("Waypoint Distance Text Scale", "Escala del texto de distancia"),
        ("Distance to WP", "Distancia al punto"), ("Waypoint Distance In Menu", "Distancia al punto en el menú"),
        ("WP Name Above Distance", "Nombre del punto sobre la distancia"), ("WP Dist. Vertic. Angle", "Ángulo vertical de distancia"),
        ("WP Dist. Horis. Angle", "Ángulo horizontal de distancia"), ("Render All Waypoint Sets", "Mostrar todos los grupos de puntos"),
        ("Deathpoints", "Puntos de muerte"), ("Keep Old Deathpoints", "Conservar puntos de muerte anteriores"),
        ("Delete Reached Deathpoints", "Eliminar puntos de muerte alcanzados"), ("Overworld Waypoints in Nether", "Puntos del Overworld en el Nether"),
        ("New Waypoints to Bottom", "Puntos nuevos al final"), ("Max WP Render Dist.", "Distancia máxima de puntos"),
        ("Waypoint Beacon Distance Min", "Distancia mínima de baliza"), ("Min WP Render Dist.", "Distancia mínima de puntos"),
        ("Convert Distance to KM at", "Convertir distancia a km desde"), ("Waypoint Distance Precision", "Precisión de distancia"),
        ("Waypoint Opacity in World", "Opacidad de puntos en el mundo"), ("Waypoint Opacity on Map", "Opacidad de puntos en el mapa"),
        ("On-Map Waypoint Scale", "Escala de puntos en el mapa"), ("North Compass Color", "Color norte de brújula"),
    ];
    const ZH_MORE: &[(&str, &str)] = &[
        ("Cave Mode Zoom In", "洞穴模式放大"), ("Zoom Level When Enlarged", "放大时的缩放级别"),
        ("Undiscovered Opacity", "未探索区域透明度"), ("Keep Rotation When Enlarged", "放大时保持旋转"),
        ("Toggled Enlarged Minimap", "切换放大小地图"), ("Minimap FPS Limiter", "小地图帧率限制"),
        ("Minimap Shows the Map's Dimension", "小地图显示当前地图维度"), ("Minimap FPS Limit", "小地图帧率上限"),
        ("Cave Mode in the Nether", "下界洞穴模式"), ("Cave Mode Depth", "洞穴模式深度"),
        ("Cave Mode Toggle Timer", "洞穴模式切换计时"), ("Terrain Depth", "地形深度"),
        ("Show Grass and Plants", "显示草和植物"), ("Show Tree Leaves", "显示树叶"),
        ("Display Stained Glass", "显示染色玻璃"), ("Transparent Obsidian Roof", "透明黑曜石屋顶"),
        ("Obsidian Roof Y", "黑曜石屋顶 Y 高度"), ("Entity Dot Size", "实体点大小"),
        ("Entity Icon Scale", "实体图标大小"), ("Entity Number", "实体数量"),
        ("Entity Height Limit", "实体高度限制"), ("Show Entity Depth", "显示实体深度"),
        ("Min Y Distance For Depth Fade", "深度淡化最小 Y 距离"), ("Display Entity Height", "显示实体高度"),
        ("Main Entity As", "主要实体显示为"), ("Arrow Scale", "箭头大小"), ("Arrow Color", "箭头颜色"),
        ("Arrow Opacity", "箭头透明度"), ("Hide Invisible Entities", "隐藏隐形实体"),
        ("Chunk Grid Line Width", "区块网格线宽"), ("Light Overlay Below Level", "低于亮度等级时显示光照"),
        ("Server View Distance", "服务器视距"), ("Chunk", "区块"), ("Weather", "天气"),
        ("Info Display Background Opacity", "信息背景透明度"), ("Info Text Alignment", "信息文本对齐"),
        ("Map Sharing Rate", "地图共享速率"), ("Server Map Folder", "服务器地图文件夹"),
        ("UI Scale", "界面缩放"), ("In-World Waypoint Icon Scale", "世界路径点图标大小"),
        ("Waypoint Name Text Scale", "路径点名称文字大小"), ("Waypoint Distance Text Scale", "路径点距离文字大小"),
        ("Distance to WP", "到路径点距离"), ("Waypoint Distance In Menu", "菜单中的路径点距离"),
        ("WP Name Above Distance", "路径点名称显示在距离上方"), ("WP Dist. Vertic. Angle", "距离垂直角度"),
        ("WP Dist. Horis. Angle", "距离水平角度"), ("Render All Waypoint Sets", "显示所有路径点组"),
        ("Deathpoints", "死亡点"), ("Keep Old Deathpoints", "保留旧死亡点"),
        ("Delete Reached Deathpoints", "删除已到达的死亡点"), ("Overworld Waypoints in Nether", "在下界显示主世界路径点"),
        ("New Waypoints to Bottom", "新路径点置底"), ("Max WP Render Dist.", "路径点最大显示距离"),
        ("Waypoint Beacon Distance Min", "路径点信标最小距离"), ("Min WP Render Dist.", "路径点最小显示距离"),
        ("Convert Distance to KM at", "距离达到此值后用公里"), ("Waypoint Distance Precision", "路径点距离精度"),
        ("Waypoint Opacity in World", "世界中路径点透明度"), ("Waypoint Opacity on Map", "地图中路径点透明度"),
        ("On-Map Waypoint Scale", "地图路径点大小"), ("North Compass Color", "指南针北方颜色"),
    ];
    const RU_MORE: &[(&str, &str)] = &[
        ("Cave Mode Zoom In", "Увеличение режима пещер"), ("Zoom Level When Enlarged", "Масштаб увеличенной карты"),
        ("Undiscovered Opacity", "Непрозрачность неизведанного"), ("Keep Rotation When Enlarged", "Сохранять поворот при увеличении"),
        ("Toggled Enlarged Minimap", "Переключение увеличенной мини-карты"), ("Minimap FPS Limiter", "Ограничение FPS мини-карты"),
        ("Minimap Shows the Map's Dimension", "Мини-карта показывает измерение карты"), ("Minimap FPS Limit", "Лимит FPS мини-карты"),
        ("Cave Mode in the Nether", "Режим пещер в Незере"), ("Cave Mode Depth", "Глубина режима пещер"),
        ("Cave Mode Toggle Timer", "Таймер переключения режима пещер"), ("Terrain Depth", "Глубина местности"),
        ("Show Grass and Plants", "Показывать траву и растения"), ("Show Tree Leaves", "Показывать листву"),
        ("Display Stained Glass", "Показывать тонированное стекло"), ("Transparent Obsidian Roof", "Прозрачная крыша из обсидиана"),
        ("Obsidian Roof Y", "Высота Y крыши из обсидиана"), ("Entity Dot Size", "Размер точки сущности"),
        ("Entity Icon Scale", "Размер значка сущности"), ("Entity Number", "Количество сущностей"),
        ("Entity Height Limit", "Ограничение высоты сущностей"), ("Show Entity Depth", "Показывать глубину сущностей"),
        ("Min Y Distance For Depth Fade", "Мин. расстояние Y для затухания глубины"), ("Display Entity Height", "Показывать высоту сущностей"),
        ("Main Entity As", "Основная сущность как"), ("Arrow Scale", "Размер стрелки"), ("Arrow Color", "Цвет стрелки"),
        ("Arrow Opacity", "Непрозрачность стрелки"), ("Hide Invisible Entities", "Скрывать невидимых сущностей"),
        ("Chunk Grid Line Width", "Толщина линий сетки чанков"), ("Light Overlay Below Level", "Слой света ниже уровня"),
        ("Server View Distance", "Дальность сервера"), ("Chunk", "Чанк"), ("Weather", "Погода"),
        ("Info Display Background Opacity", "Непрозрачность фона информации"), ("Info Text Alignment", "Выравнивание текста информации"),
        ("Map Sharing Rate", "Скорость обмена картой"), ("Server Map Folder", "Папка карты сервера"),
        ("UI Scale", "Масштаб интерфейса"), ("In-World Waypoint Icon Scale", "Размер значка метки в мире"),
        ("Waypoint Name Text Scale", "Размер текста названия метки"), ("Waypoint Distance Text Scale", "Размер текста расстояния до метки"),
        ("Distance to WP", "Расстояние до метки"), ("Waypoint Distance In Menu", "Расстояние до метки в меню"),
        ("WP Name Above Distance", "Название метки над расстоянием"), ("WP Dist. Vertic. Angle", "Вертикальный угол расстояния"),
        ("WP Dist. Horis. Angle", "Горизонтальный угол расстояния"), ("Render All Waypoint Sets", "Показывать все наборы меток"),
        ("Deathpoints", "Точки смерти"), ("Keep Old Deathpoints", "Хранить старые точки смерти"),
        ("Delete Reached Deathpoints", "Удалять достигнутые точки смерти"), ("Overworld Waypoints in Nether", "Метки Верхнего мира в Незере"),
        ("New Waypoints to Bottom", "Новые метки внизу"), ("Max WP Render Dist.", "Макс. дальность меток"),
        ("Waypoint Beacon Distance Min", "Мин. расстояние маяка метки"), ("Min WP Render Dist.", "Мин. дальность меток"),
        ("Convert Distance to KM at", "Переводить в км от"), ("Waypoint Distance Precision", "Точность расстояния"),
        ("Waypoint Opacity in World", "Непрозрачность меток в мире"), ("Waypoint Opacity on Map", "Непрозрачность меток на карте"),
        ("On-Map Waypoint Scale", "Размер метки на карте"), ("North Compass Color", "Цвет севера компаса"),
    ];
    const PL_MORE: &[(&str, &str)] = &[
        ("Cave Mode Zoom In", "Powiększenie trybu jaskiń"), ("Zoom Level When Enlarged", "Poziom powiększenia"),
        ("Undiscovered Opacity", "Nieodkryta przezroczystość"), ("Keep Rotation When Enlarged", "Zachowaj obrót po powiększeniu"),
        ("Toggled Enlarged Minimap", "Przełącz powiększoną minimapę"), ("Minimap FPS Limiter", "Limit FPS minimapy"),
        ("Minimap Shows the Map's Dimension", "Minimapa pokazuje wymiar mapy"), ("Minimap FPS Limit", "Limit FPS minimapy"),
        ("Cave Mode in the Nether", "Tryb jaskiń w Netherze"), ("Cave Mode Depth", "Głębokość trybu jaskiń"),
        ("Cave Mode Toggle Timer", "Czas przełączania trybu jaskiń"), ("Terrain Depth", "Głębokość terenu"),
        ("Show Grass and Plants", "Pokaż trawę i rośliny"), ("Show Tree Leaves", "Pokaż liście drzew"),
        ("Display Stained Glass", "Pokaż barwione szkło"), ("Transparent Obsidian Roof", "Przezroczysty dach z obsydianu"),
        ("Obsidian Roof Y", "Wysokość Y dachu z obsydianu"), ("Entity Dot Size", "Rozmiar punktu bytu"),
        ("Entity Icon Scale", "Skala ikon bytów"), ("Entity Number", "Liczba bytów"),
        ("Entity Height Limit", "Limit wysokości bytów"), ("Show Entity Depth", "Pokaż głębokość bytów"),
        ("Min Y Distance For Depth Fade", "Min. odległość Y zanikania głębi"), ("Display Entity Height", "Pokaż wysokość bytów"),
        ("Main Entity As", "Główny byt jako"), ("Arrow Scale", "Skala strzałki"), ("Arrow Color", "Kolor strzałki"),
        ("Arrow Opacity", "Nieprzezroczystość strzałki"), ("Hide Invisible Entities", "Ukryj niewidzialne byty"),
        ("Chunk Grid Line Width", "Grubość linii siatki chunków"), ("Light Overlay Below Level", "Nakładka światła poniżej poziomu"),
        ("Server View Distance", "Zasięg serwera"), ("Chunk", "Chunk"), ("Weather", "Pogoda"),
        ("Info Display Background Opacity", "Nieprzezroczystość tła informacji"), ("Info Text Alignment", "Wyrównanie tekstu informacji"),
        ("Map Sharing Rate", "Szybkość udostępniania mapy"), ("Server Map Folder", "Folder mapy serwera"),
        ("UI Scale", "Skala interfejsu"), ("In-World Waypoint Icon Scale", "Skala ikony punktu w świecie"),
        ("Waypoint Name Text Scale", "Skala tekstu nazwy punktu"), ("Waypoint Distance Text Scale", "Skala tekstu odległości punktu"),
        ("Distance to WP", "Odległość do punktu"), ("Waypoint Distance In Menu", "Odległość punktu w menu"),
        ("WP Name Above Distance", "Nazwa punktu nad odległością"), ("WP Dist. Vertic. Angle", "Kąt pionowy odległości"),
        ("WP Dist. Horis. Angle", "Kąt poziomy odległości"), ("Render All Waypoint Sets", "Wyświetl wszystkie zestawy punktów"),
        ("Deathpoints", "Punkty śmierci"), ("Keep Old Deathpoints", "Zachowaj stare punkty śmierci"),
        ("Delete Reached Deathpoints", "Usuń osiągnięte punkty śmierci"), ("Overworld Waypoints in Nether", "Punkty Nadziemia w Netherze"),
        ("New Waypoints to Bottom", "Nowe punkty na dole"), ("Max WP Render Dist.", "Maks. zasięg punktów"),
        ("Waypoint Beacon Distance Min", "Min. odległość latarni punktu"), ("Min WP Render Dist.", "Min. zasięg punktów"),
        ("Convert Distance to KM at", "Zamień odległość na km od"), ("Waypoint Distance Precision", "Dokładność odległości"),
        ("Waypoint Opacity in World", "Nieprzezroczystość punktów w świecie"), ("Waypoint Opacity on Map", "Nieprzezroczystość punktów na mapie"),
        ("On-Map Waypoint Scale", "Skala punktu na mapie"), ("North Compass Color", "Kolor północy kompasu"),
    ];
    const UK_MORE: &[(&str, &str)] = &[
        ("Cave Mode Zoom In", "Збільшення режиму печер"), ("Zoom Level When Enlarged", "Рівень масштабування"),
        ("Undiscovered Opacity", "Непрозорість невідомого"), ("Keep Rotation When Enlarged", "Зберігати обертання при збільшенні"),
        ("Toggled Enlarged Minimap", "Перемикати збільшену мінімапу"), ("Minimap FPS Limiter", "Обмеження FPS мінімапи"),
        ("Minimap Shows the Map's Dimension", "Мінімапа показує вимір мапи"), ("Minimap FPS Limit", "Ліміт FPS мінімапи"),
        ("Cave Mode in the Nether", "Режим печер у Незері"), ("Cave Mode Depth", "Глибина режиму печер"),
        ("Cave Mode Toggle Timer", "Час перемикання режиму печер"), ("Terrain Depth", "Глибина місцевості"),
        ("Show Grass and Plants", "Показувати траву та рослини"), ("Show Tree Leaves", "Показувати листя"),
        ("Display Stained Glass", "Показувати тоноване скло"), ("Transparent Obsidian Roof", "Прозорий дах з обсидіану"),
        ("Obsidian Roof Y", "Висота Y даху з обсидіану"), ("Entity Dot Size", "Розмір точки сутності"),
        ("Entity Icon Scale", "Розмір значків сутностей"), ("Entity Number", "Кількість сутностей"),
        ("Entity Height Limit", "Обмеження висоти сутностей"), ("Show Entity Depth", "Показувати глибину сутностей"),
        ("Min Y Distance For Depth Fade", "Мін. відстань Y для згасання глибини"), ("Display Entity Height", "Показувати висоту сутностей"),
        ("Main Entity As", "Основна сутність як"), ("Arrow Scale", "Розмір стрілки"), ("Arrow Color", "Колір стрілки"),
        ("Arrow Opacity", "Непрозорість стрілки"), ("Hide Invisible Entities", "Ховати невидимих сутностей"),
        ("Chunk Grid Line Width", "Товщина ліній сітки чанків"), ("Light Overlay Below Level", "Шар світла нижче рівня"),
        ("Server View Distance", "Дальність сервера"), ("Chunk", "Чанк"), ("Weather", "Погода"),
        ("Info Display Background Opacity", "Непрозорість фону інформації"), ("Info Text Alignment", "Вирівнювання тексту інформації"),
        ("Map Sharing Rate", "Швидкість обміну мапою"), ("Server Map Folder", "Тека мапи сервера"),
        ("UI Scale", "Масштаб інтерфейсу"), ("In-World Waypoint Icon Scale", "Розмір значка точки у світі"),
        ("Waypoint Name Text Scale", "Розмір тексту назви точки"), ("Waypoint Distance Text Scale", "Розмір тексту відстані до точки"),
        ("Distance to WP", "Відстань до точки"), ("Waypoint Distance In Menu", "Відстань до точки в меню"),
        ("WP Name Above Distance", "Назва точки над відстанню"), ("WP Dist. Vertic. Angle", "Вертикальний кут відстані"),
        ("WP Dist. Horis. Angle", "Горизонтальний кут відстані"), ("Render All Waypoint Sets", "Показувати всі набори точок"),
        ("Deathpoints", "Точки смерті"), ("Keep Old Deathpoints", "Зберігати старі точки смерті"),
        ("Delete Reached Deathpoints", "Видаляти досягнуті точки смерті"), ("Overworld Waypoints in Nether", "Точки Верхнього світу в Незері"),
        ("New Waypoints to Bottom", "Нові точки внизу"), ("Max WP Render Dist.", "Макс. дальність точок"),
        ("Waypoint Beacon Distance Min", "Мін. відстань маяка точки"), ("Min WP Render Dist.", "Мін. дальність точок"),
        ("Convert Distance to KM at", "Переводити в км від"), ("Waypoint Distance Precision", "Точність відстані"),
        ("Waypoint Opacity in World", "Непрозорість точок у світі"), ("Waypoint Opacity on Map", "Непрозорість точок на мапі"),
        ("On-Map Waypoint Scale", "Розмір точки на мапі"), ("North Compass Color", "Колір півночі компаса"),
    ];
    const TR_MORE: &[(&str, &str)] = &[
        ("Cave Mode Zoom In", "Mağara modu yakınlaştırma"), ("Zoom Level When Enlarged", "Büyütme düzeyi"),
        ("Undiscovered Opacity", "Keşfedilmemiş alan opaklığı"), ("Keep Rotation When Enlarged", "Büyütülünce dönüşü koru"),
        ("Toggled Enlarged Minimap", "Büyütülmüş mini haritayı değiştir"), ("Minimap FPS Limiter", "Mini harita FPS sınırı"),
        ("Minimap Shows the Map's Dimension", "Mini harita, haritanın boyutunu gösterir"), ("Minimap FPS Limit", "Mini harita FPS sınırı"),
        ("Cave Mode in the Nether", "Nether'da mağara modu"), ("Cave Mode Depth", "Mağara modu derinliği"),
        ("Cave Mode Toggle Timer", "Mağara modu geçiş süresi"), ("Terrain Depth", "Arazi derinliği"),
        ("Show Grass and Plants", "Otları ve bitkileri göster"), ("Show Tree Leaves", "Ağaç yapraklarını göster"),
        ("Display Stained Glass", "Renkli camı göster"), ("Transparent Obsidian Roof", "Saydam obsidyen çatısı"),
        ("Obsidian Roof Y", "Obsidyen çatısı Y yüksekliği"), ("Entity Dot Size", "Varlık nokta boyutu"),
        ("Entity Icon Scale", "Varlık simgesi boyutu"), ("Entity Number", "Varlık sayısı"),
        ("Entity Height Limit", "Varlık yükseklik sınırı"), ("Show Entity Depth", "Varlık derinliğini göster"),
        ("Min Y Distance For Depth Fade", "Derinlik solması için en az Y mesafesi"), ("Display Entity Height", "Varlık yüksekliğini göster"),
        ("Main Entity As", "Ana varlığı şöyle göster"), ("Arrow Scale", "Ok boyutu"), ("Arrow Color", "Ok rengi"),
        ("Arrow Opacity", "Ok opaklığı"), ("Hide Invisible Entities", "Görünmez varlıkları gizle"),
        ("Chunk Grid Line Width", "Chunk ızgara çizgi kalınlığı"), ("Light Overlay Below Level", "Şu seviyenin altında ışık katmanı"),
        ("Server View Distance", "Sunucu görüş mesafesi"), ("Chunk", "Chunk"), ("Weather", "Hava durumu"),
        ("Info Display Background Opacity", "Bilgi arka planı opaklığı"), ("Info Text Alignment", "Bilgi metni hizası"),
        ("Map Sharing Rate", "Harita paylaşım hızı"), ("Server Map Folder", "Sunucu harita klasörü"),
        ("UI Scale", "Arayüz ölçeği"), ("In-World Waypoint Icon Scale", "Dünyadaki işaretçi simge boyutu"),
        ("Waypoint Name Text Scale", "İşaretçi adı yazı boyutu"), ("Waypoint Distance Text Scale", "İşaretçi mesafe yazı boyutu"),
        ("Distance to WP", "İşaretçiye mesafe"), ("Waypoint Distance In Menu", "Menüde işaretçi mesafesi"),
        ("WP Name Above Distance", "İşaretçi adı mesafenin üstünde"), ("WP Dist. Vertic. Angle", "Dikey mesafe açısı"),
        ("WP Dist. Horis. Angle", "Yatay mesafe açısı"), ("Render All Waypoint Sets", "Tüm işaretçi gruplarını göster"),
        ("Deathpoints", "Ölüm noktaları"), ("Keep Old Deathpoints", "Eski ölüm noktalarını tut"),
        ("Delete Reached Deathpoints", "Ulaşılan ölüm noktalarını sil"), ("Overworld Waypoints in Nether", "Nether'da Üst Dünya işaretçileri"),
        ("New Waypoints to Bottom", "Yeni işaretçileri alta ekle"), ("Max WP Render Dist.", "En yüksek işaretçi mesafesi"),
        ("Waypoint Beacon Distance Min", "İşaretçi ışını en az mesafesi"), ("Min WP Render Dist.", "En düşük işaretçi mesafesi"),
        ("Convert Distance to KM at", "Şu mesafeden sonra km kullan"), ("Waypoint Distance Precision", "İşaretçi mesafe hassasiyeti"),
        ("Waypoint Opacity in World", "Dünyada işaretçi opaklığı"), ("Waypoint Opacity on Map", "Haritada işaretçi opaklığı"),
        ("On-Map Waypoint Scale", "Haritadaki işaretçi boyutu"), ("North Compass Color", "Kuzey pusula rengi"),
    ];
    const ES_UI: &[(&str, &str)] = &[
        ("Player List", "Lista de jugadores"), ("No Names", "Sin nombres"),
        ("3x3 Ceiling", "Techo 3x3"), ("1x1 Ceiling", "Techo 1x1"),
        ("Server Name", "Nombre del servidor"), ("Base Domain", "Dominio base"),
        ("Looking At", "Mirando a"), ("Top Left", "Arriba izquierda"), ("Top Right", "Arriba derecha"),
        ("Bottom Left", "Abajo izquierda"), ("Bottom Right", "Abajo derecha"),
        ("Map", "Mapa"), ("Day", "Día"), ("Waypoint", "Punto de ruta"),
        ("Overworld", "Mundo normal"), ("Nether", "Nether"), ("The End", "El End"),
        ("Surface", "Superficie"), ("Cave", "Cueva"), ("Layer", "Capa"), ("All", "Todos"),
        ("<< Prev", "<< Anterior"), ("Next >>", "Siguiente >>"),
        ("Coordinates must be whole numbers", "Las coordenadas deben ser números enteros"),
    ];
    const ZH_UI: &[(&str, &str)] = &[
        ("Player List", "玩家列表"), ("No Names", "无名称"), ("3x3 Ceiling", "3x3 天花板"),
        ("1x1 Ceiling", "1x1 天花板"), ("Server Name", "服务器名称"), ("Base Domain", "基础域名"),
        ("Looking At", "正在看向"), ("Top Left", "左上"), ("Top Right", "右上"),
        ("Bottom Left", "左下"), ("Bottom Right", "右下"), ("Map", "地图"), ("Day", "第"),
        ("Waypoint", "路径点"), ("Overworld", "主世界"), ("Nether", "下界"), ("The End", "末地"),
        ("Surface", "地表"), ("Cave", "洞穴"), ("Layer", "层"), ("All", "全部"),
        ("<< Prev", "<< 上一页"), ("Next >>", "下一页 >>"),
        ("Coordinates must be whole numbers", "坐标必须是整数"),
    ];
    const RU_UI: &[(&str, &str)] = &[
        ("Player List", "Список игроков"), ("No Names", "Без имён"), ("3x3 Ceiling", "Потолок 3x3"),
        ("1x1 Ceiling", "Потолок 1x1"), ("Server Name", "Имя сервера"), ("Base Domain", "Основной домен"),
        ("Looking At", "Смотрит на"), ("Top Left", "Сверху слева"), ("Top Right", "Сверху справа"),
        ("Bottom Left", "Снизу слева"), ("Bottom Right", "Снизу справа"), ("Map", "Карта"), ("Day", "День"),
        ("Waypoint", "Метка"), ("Overworld", "Верхний мир"), ("Nether", "Незер"), ("The End", "Энд"),
        ("Surface", "Поверхность"), ("Cave", "Пещера"), ("Layer", "Слой"), ("All", "Все"),
        ("<< Prev", "<< Назад"), ("Next >>", "Далее >>"),
        ("Coordinates must be whole numbers", "Координаты должны быть целыми числами"),
    ];
    const PL_UI: &[(&str, &str)] = &[
        ("Player List", "Lista graczy"), ("No Names", "Bez nazw"), ("3x3 Ceiling", "Sufit 3x3"),
        ("1x1 Ceiling", "Sufit 1x1"), ("Server Name", "Nazwa serwera"), ("Base Domain", "Domena bazowa"),
        ("Looking At", "Patrzy na"), ("Top Left", "Lewy górny"), ("Top Right", "Prawy górny"),
        ("Bottom Left", "Lewy dolny"), ("Bottom Right", "Prawy dolny"), ("Map", "Mapa"), ("Day", "Dzień"),
        ("Waypoint", "Punkt trasy"), ("Overworld", "Nadziemie"), ("Nether", "Nether"), ("The End", "Kres"),
        ("Surface", "Powierzchnia"), ("Cave", "Jaskinia"), ("Layer", "Warstwa"), ("All", "Wszystkie"),
        ("<< Prev", "<< Poprzednia"), ("Next >>", "Następna >>"),
        ("Coordinates must be whole numbers", "Współrzędne muszą być liczbami całkowitymi"),
    ];
    const UK_UI: &[(&str, &str)] = &[
        ("Player List", "Список гравців"), ("No Names", "Без імен"), ("3x3 Ceiling", "Стеля 3x3"),
        ("1x1 Ceiling", "Стеля 1x1"), ("Server Name", "Назва сервера"), ("Base Domain", "Базовий домен"),
        ("Looking At", "Дивиться на"), ("Top Left", "Зверху ліворуч"), ("Top Right", "Зверху праворуч"),
        ("Bottom Left", "Знизу ліворуч"), ("Bottom Right", "Знизу праворуч"), ("Map", "Мапа"), ("Day", "День"),
        ("Waypoint", "Точка маршруту"), ("Overworld", "Верхній світ"), ("Nether", "Незер"), ("The End", "Енд"),
        ("Surface", "Поверхня"), ("Cave", "Печера"), ("Layer", "Шар"), ("All", "Усі"),
        ("<< Prev", "<< Попередня"), ("Next >>", "Наступна >>"),
        ("Coordinates must be whole numbers", "Координати мають бути цілими числами"),
    ];
    const TR_UI: &[(&str, &str)] = &[
        ("Player List", "Oyuncu listesi"), ("No Names", "İsim yok"), ("3x3 Ceiling", "3x3 tavan"),
        ("1x1 Ceiling", "1x1 tavan"), ("Server Name", "Sunucu adı"), ("Base Domain", "Temel etki alanı"),
        ("Looking At", "Bakılan"), ("Top Left", "Sol üst"), ("Top Right", "Sağ üst"),
        ("Bottom Left", "Sol alt"), ("Bottom Right", "Sağ alt"), ("Map", "Harita"), ("Day", "Gün"),
        ("Waypoint", "İşaretçi"), ("Overworld", "Üst Dünya"), ("Nether", "Nether"), ("The End", "Son"),
        ("Surface", "Yüzey"), ("Cave", "Mağara"), ("Layer", "Katman"), ("All", "Tümü"),
        ("<< Prev", "<< Önceki"), ("Next >>", "Sonraki >>"),
        ("Coordinates must be whole numbers", "Koordinatlar tam sayı olmalıdır"),
    ];
    let table = match language {
        "es_es" => ES,
        "zh_cn" => ZH,
        "ru_ru" => RU,
        "pl_pl" => PL,
        "uk_ua" => UK,
        "tr_tr" => TR,
        _ => &[][..],
    };
    let extra = match language {
        "es_es" => ES_EXTRA,
        "zh_cn" => ZH_EXTRA,
        "ru_ru" => RU_EXTRA,
        "pl_pl" => PL_EXTRA,
        "uk_ua" => UK_EXTRA,
        "tr_tr" => TR_EXTRA,
        _ => &[][..],
    };
    let more = match language {
        "es_es" => ES_MORE,
        "zh_cn" => ZH_MORE,
        "ru_ru" => RU_MORE,
        "pl_pl" => PL_MORE,
        "uk_ua" => UK_MORE,
        "tr_tr" => TR_MORE,
        _ => &[][..],
    };
    let ui = match language {
        "es_es" => ES_UI,
        "zh_cn" => ZH_UI,
        "ru_ru" => RU_UI,
        "pl_pl" => PL_UI,
        "uk_ua" => UK_UI,
        "tr_tr" => TR_UI,
        _ => &[][..],
    };
    if let Some((_, translated)) = table.iter().chain(extra.iter()).chain(more.iter()).chain(ui.iter()).find(|(source, _)| *source == text) {
        return (*translated).to_owned();
    }
    if let Some((title, suffix)) = text.split_once(" (") {
        let translated = atlas_localized_text(title, language);
        if translated != title {
            return format!("{translated} ({suffix}");
        }
    }
    if let Some((label, value)) = text.split_once(": ")
        && let Some((_, translated)) = table.iter().chain(extra.iter()).chain(more.iter()).chain(ui.iter()).find(|(source, _)| *source == label)
    {
        return format!("{translated}: {}", atlas_localized_text(value, language));
    }
    if let Some(rest) = text.strip_prefix("Waypoint ") {
        return format!("{} {rest}", atlas_localized_text("Waypoint", language));
    }
    if let Some(rest) = text.strip_prefix("Day ") {
        return format!("{} {rest}", atlas_localized_text("Day", language));
    }
    if let Some((above, below)) = text.split_once(" above, ")
        && let Some(below) = below.strip_suffix(" below")
    {
        let (above_label, below_label) = match language {
            "es_es" => ("arriba", "abajo"),
            "zh_cn" => ("上方", "下方"),
            "ru_ru" => ("выше", "ниже"),
            "pl_pl" => ("wyżej", "niżej"),
            "uk_ua" => ("вище", "нижче"),
            "tr_tr" => ("yukarıda", "aşağıda"),
            _ => ("above", "below"),
        };
        return format!("{above} {above_label}, {below} {below_label}");
    }
    if let Some(base) = text.strip_suffix(" (deathpoint)") {
        let suffix = match language {
            "es_es" => " (punto de muerte)",
            "zh_cn" => "（死亡点）",
            "ru_ru" => " (точка смерти)",
            "pl_pl" => " (punkt śmierci)",
            "uk_ua" => " (точка смерci)",
            "tr_tr" => " (ölüm noktası)",
            _ => " (deathpoint)",
        };
        return format!("{base}{suffix}");
    }
    if let Some(base) = text.strip_suffix(" (temporary)") {
        let suffix = match language {
            "es_es" => " (temporal)",
            "zh_cn" => "（临时）",
            "ru_ru" => " (временная)",
            "pl_pl" => " (tymczasowy)",
            "uk_ua" => " (тимчасова)",
            "tr_tr" => " (geçici)",
            _ => " (temporary)",
        };
        return format!("{base}{suffix}");
    }
    if let Some(base) = text.strip_suffix("  (disabled)") {
        let suffix = match language {
            "es_es" => "  (desactivado)",
            "zh_cn" => "  (已禁用)",
            "ru_ru" => "  (отключено)",
            "pl_pl" => "  (wyłączono)",
            "uk_ua" => "  (вимкнено)",
            "tr_tr" => "  (devre dışı)",
            _ => "  (disabled)",
        };
        return format!("{base}{suffix}");
    }
    if let Some(rest) = text.strip_prefix("Click to edit ") {
        let prefix = match language {
            "es_es" => "Editar: ",
            "zh_cn" => "编辑：",
            "ru_ru" => "Изменить: ",
            "pl_pl" => "Edytuj: ",
            "uk_ua" => "Змінити: ",
            "tr_tr" => "Düzenle: ",
            _ => "Click to edit ",
        };
        return format!("{prefix}{rest}");
    }
    text.to_owned()
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
