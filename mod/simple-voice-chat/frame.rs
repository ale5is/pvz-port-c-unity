fn frame(game: &mut crate::game::Game<'_>, elapsed: std::time::Duration) {
    crate::game::simple_voice_chat::on_frame(game, elapsed);
    for (channel, data) in crate::game::simple_voice_chat::drain_outbound() {
        game.send(horizon_protocol::packets::play::Serverbound::CustomPayload(
            horizon_protocol::packets::configuration::CustomPayload { channel, data },
        ));
    }
}
