fn simple_voice_chat_settings(
    game: &mut crate::game::Game<'_>,
    mod_id: &str,
    buttons: &mut Vec<String>,
) {
    crate::game::simple_voice_chat::settings(game, mod_id, buttons);
}
