fn register_native_mods(
    game: &mut crate::game::Game<'_>,
    mods: &mut Vec<crate::game::native_hooks::NativeMod>,
) {
    crate::game::simple_voice_chat::register_mod(mods);
    crate::game::jade::register_mod(mods);
    let _ = game;
}
