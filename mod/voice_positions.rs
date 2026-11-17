impl crate::game::Game<'_> {
    pub(in crate::game) fn native_voice_positions(
        &self,
    ) -> (Option<[f64; 3]>, Vec<(String, [f64; 3])>) {
        let listener = self
            .player
            .as_ref()
            .map(|player| [player.position.x, player.position.y, player.position.z]);
        let Some(remote) = self.remote.as_ref() else {
            return (listener, Vec::new());
        };
        let players = self
            .remote_player_names()
            .into_iter()
            .filter_map(|(entity_id, name)| {
                remote
                    .tracked(entity_id as i32)
                    .map(|(_, position)| (name, position))
            })
            .collect();
        (listener, players)
    }
}
