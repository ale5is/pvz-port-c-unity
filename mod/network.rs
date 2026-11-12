pub fn send_payload(
    game: &mut crate::game::Game<'_>,
    channel: &str,
    data: &[u8],
) {
    game.send(horizon_protocol::packets::play::Serverbound::CustomPayload(
        horizon_protocol::packets::configuration::CustomPayload {
            channel: channel.to_owned(),
            data: data.to_vec(),
        },
    ));
}
