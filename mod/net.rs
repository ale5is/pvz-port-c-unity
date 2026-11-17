fn net_from_server(
    game: &mut crate::game::Game<'_>,
    channel: &str,
    data: &[u8],
) {
    crate::game::simple_voice_chat::on_net_from_game(game, channel, data);
}
