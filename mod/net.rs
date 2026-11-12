fn net_from_server(game: &mut crate::game::Game<'_>, channel: &str, data: &[u8]) {
    let server_address = game
        .remote
        .as_ref()
        .map(|remote| remote.session.address.as_str());
    crate::game::simple_voice_chat::on_net(channel, data, server_address);
}
