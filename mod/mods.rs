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
        ],
    });
    let _ = game;
}
