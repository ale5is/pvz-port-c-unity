fn register_native_mods(
    game: &mut crate::game::Game<'_>,
    mods: &mut Vec<crate::game::native_hooks::NativeMod>,
) {
    crate::game::simple_voice_chat::register_mod(mods);
    crate::game::jade::register_mod(mods);
    mods.push(crate::game::native_hooks::NativeMod {
        id: crate::game::atlas_mod::ID.to_owned(),
        title: format!("{} {}", crate::game::atlas_mod::NAME, crate::game::atlas_mod::VERSION),
        description: crate::game::atlas_mod::DESCRIPTION.to_owned(),
        keys: vec![
            ["map".to_owned(), "World Map".to_owned(), crate::game::atlas_mod::DEFAULT_MAP.to_owned()],
            ["create".to_owned(), "New Waypoint".to_owned(), crate::game::atlas_mod::DEFAULT_CREATE.to_owned()],
            ["list".to_owned(), "Waypoint List".to_owned(), crate::game::atlas_mod::DEFAULT_LIST.to_owned()],
            ["settings".to_owned(), "Atlas Settings".to_owned(), crate::game::atlas_mod::DEFAULT_SETTINGS.to_owned()],
            ["temporary".to_owned(), "Temporary Waypoint".to_owned(), crate::game::atlas_mod::DEFAULT_TEMP.to_owned()],
            ["enlarge".to_owned(), "Enlarge Minimap".to_owned(), crate::game::atlas_mod::DEFAULT_ENLARGE.to_owned()],
            ["zoom_in".to_owned(), "Zoom In".to_owned(), crate::game::atlas_mod::DEFAULT_ZOOM_IN.to_owned()],
            ["zoom_out".to_owned(), "Zoom Out".to_owned(), crate::game::atlas_mod::DEFAULT_ZOOM_OUT.to_owned()],
            ["dimension".to_owned(), "Change Map Dimension".to_owned(), crate::game::atlas_mod::DEFAULT_DIMENSION.to_owned()],
            ["pan_up".to_owned(), "Pan Map Up".to_owned(), crate::game::atlas_mod::DEFAULT_PAN_UP.to_owned()],
            ["pan_down".to_owned(), "Pan Map Down".to_owned(), crate::game::atlas_mod::DEFAULT_PAN_DOWN.to_owned()],
            ["pan_left".to_owned(), "Pan Map Left".to_owned(), crate::game::atlas_mod::DEFAULT_PAN_LEFT.to_owned()],
            ["pan_right".to_owned(), "Pan Map Right".to_owned(), crate::game::atlas_mod::DEFAULT_PAN_RIGHT.to_owned()],
            ["world_zoom_in".to_owned(), "World Map Zoom In".to_owned(), crate::game::atlas_mod::DEFAULT_WORLD_ZOOM_IN.to_owned()],
            ["world_zoom_out".to_owned(), "World Map Zoom Out".to_owned(), crate::game::atlas_mod::DEFAULT_WORLD_ZOOM_OUT.to_owned()],
            ["centre".to_owned(), "Centre Map".to_owned(), crate::game::atlas_mod::DEFAULT_CENTRE.to_owned()],
            ["draw".to_owned(), "Toggle Drawing".to_owned(), crate::game::atlas_mod::DEFAULT_DRAW.to_owned()],
            ["undo".to_owned(), "Undo Drawing".to_owned(), crate::game::atlas_mod::DEFAULT_UNDO.to_owned()],
            ["teleport".to_owned(), "Teleport Selected Waypoint".to_owned(), crate::game::atlas_mod::DEFAULT_TELEPORT.to_owned()],
        ],
    });
    let _ = game;
}
