impl crate::game::Game<'_> {
    pub(in crate::game) fn native_voice_positions(
        &self,
    ) -> (Option<[f64; 3]>, Vec<(String, [f64; 3])>) {
        let listener = self
            .player
            .as_ref()
            .map(|player| [player.position.x, player.position.y, player.position.z]);
        let players = self
            .remote
            .as_ref()
            .map(|remote| remote.voice_player_positions())
            .unwrap_or_default();
        (listener, players)
    }
}
