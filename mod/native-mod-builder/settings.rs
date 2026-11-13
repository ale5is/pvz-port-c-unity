fn settings(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    buttons: &mut Vec<String>,
) {
    crate::game::native_mod_builder::settings(game, mod_id, buttons);
}
