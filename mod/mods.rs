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
            ["enlarge".to_owned(), "Enlarge Minimap".to_owned(), crate::game::atlas_mod::DEFAULT_ENLARGE.to_owned()],
            ["player_list".to_owned(), "Show Player List".to_owned(), "key.keyboard.tab".to_owned()],
        ],
    });
    let _ = game;
}
