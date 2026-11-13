use horizon_protocol::packets::play::Clientbound;

pub fn handle_packet(
    game: &mut crate::game::Game<'_>,
    packet: &Clientbound,
) {
    let Clientbound::CustomPayload(payload) = packet else {
        return;
    };
    if !payload.channel.starts_with("voicechat:") {
        return;
    }
    let address = crate::game::simple_voice_chat::server_address(game);
    crate::game::simple_voice_chat::on_net(&payload.channel, &payload.data, address);
}
