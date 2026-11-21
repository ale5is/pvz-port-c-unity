fn frame(game: &mut crate::game::Game<'_>, elapsed: std::time::Duration) {
    crate::game::simple_voice_chat::on_frame(game, elapsed);
    let has_horizon_mod_net = game.remote.as_ref().is_some_and(|remote| remote.mod_net);
    for (channel, data) in crate::game::simple_voice_chat::drain_outbound() {
        eprintln!("[PavoHorizon/SVC] GAME TX channel={} bytes={} transport={}", channel, data.len(), if has_horizon_mod_net { "horizon-mod-net" } else { "custom-payload" });
        if has_horizon_mod_net {
            if let Err(error) = game.native_net_send_to_server(&channel, &data) {
                eprintln!("[PavoHorizon/SVC] mod-net send failed: {error}");
            }
        } else {
            crate::game::remote::voicechat_network::send_payload(game, &channel, &data);
        }
    }
}
