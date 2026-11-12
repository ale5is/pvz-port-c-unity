fn net_from_server(channel: &str, data: &[u8]) {
    crate::game::simple_voice_chat::on_net(channel, data);
}
