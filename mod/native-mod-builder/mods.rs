fn append_builder(
    game: &mut crate::game::Game<'_>,
    mods: &mut Vec<crate::game::native_hooks::NativeMod>,
) {
    crate::game::native_mod_builder::add_mod(game, mods);
}
