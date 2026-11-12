fn net_from_server(game: &mut crate::game::Game<'_>, channel: &str, data: &[u8]) {
    let server_address = crate::game::simple_voice_chat::server_address(game);
    crate::game::simple_voice_chat::on_net(channel, data, server_address);
}
