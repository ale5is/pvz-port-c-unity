fn settings_click(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    button: usize,
) {
    crate::game::native_mod_builder::settings_click(game, mod_id, button);
}
