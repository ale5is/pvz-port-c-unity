fn simple_voice_chat_settings_click(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    button: usize,
) {
    crate::game::simple_voice_chat::settings_click(game, mod_id, button);
}
