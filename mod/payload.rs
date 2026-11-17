fn voicechat_payload(
    game: &mut crate::game::Game<'_>,
    packet: &horizon_protocol::packets::play::Clientbound,
) {
    if let horizon_protocol::packets::play::Clientbound::CustomPayload(payload) = packet {
        if payload.channel.starts_with("voicechat:") {
            eprintln!("[PavoHorizon/SVC] PACKET_HOOK channel={} bytes={}", payload.channel, payload.data.len());
            crate::game::simple_voice_chat::on_net_from_game(
                game,
                &payload.channel,
                &payload.data,
            );
        }
    }
}
