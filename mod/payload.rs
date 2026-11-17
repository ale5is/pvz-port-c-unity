fn voicechat_payload(game: &mut crate::game::Game<'_>, packet: &horizon_protocol::packets::play::Clientbound) {
    if let horizon_protocol::packets::play::Clientbound::CustomPayload(payload) = packet {
        if payload.channel.starts_with("voicechat:") {
            let address = crate::game::simple_voice_chat::server_address(game);
            crate::game::simple_voice_chat::on_net(&payload.channel, &payload.data, address);
        }
    }
}
