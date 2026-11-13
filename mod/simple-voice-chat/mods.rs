fn register_simple_voice_chat(
    game: &mut crate::game::Game<'_>,
    mods: &mut Vec<crate::game::native_hooks::NativeMod>,
) {
    crate::game::simple_voice_chat::register_mod(game, mods);
}
