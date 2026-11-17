fn svc_command(game: &crate::game::Game<'_>, command: &str, ran: &mut bool) {
    crate::game::simple_voice_chat::on_command(game, command, ran);
}
