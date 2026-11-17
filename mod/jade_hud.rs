fn jade_hud(game: &mut crate::game::Game<'_>, batch: &mut horizon_client::hud::HudBatch) {
    if !game.native_on("jade") {
        return;
    }
    crate::game::jade::hud(game, batch);
}
