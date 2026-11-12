fn command(command: &str, ran: &mut bool) {
    crate::game::simple_voice_chat::on_command(command, ran);
}
