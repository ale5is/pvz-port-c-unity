fn frame(game: &mut crate::game::Game<'_>, elapsed: std::time::Duration) {
    crate::game::simple_voice_chat::on_frame(game, elapsed);
    let has_horizon_mod_net = game.remote.as_ref().is_some_and(|remote| remote.mod_net);
    for (channel, data) in crate::game::simple_voice_chat::drain_outbound() {
        crate::game::remote::voicechat_network::send_payload(game, &channel, &data);
        if has_horizon_mod_net {
            let _ = game.native_net_send_to_server(&channel, &data);
        }
    }
}
