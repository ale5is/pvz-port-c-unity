fn atlas_android_ime_hook(game: &mut crate::game::Game<'_>) {
    crate::game::atlas_android_ime::poll(game);
}
