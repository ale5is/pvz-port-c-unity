impl super::Game<'_> {
    pub(in crate::game) fn native_voice_positions(
        &self,
    ) -> (Option<[f64; 3]>, Vec<(u128, [f64; 3])>) {
        let listener = self
            .player
            .as_ref()
            .map(|player| [player.position.x, player.position.y, player.position.z]);
        let Some(remote) = self.remote.as_ref() else {
            return (listener, Vec::new());
        };
        let players = remote
            .level_players
            .iter()
            .filter_map(|(entity_id, uuid)| {
                if *uuid == remote.profile {
                    return None;
                }
                remote
                    .entities
                    .get(entity_id)
                    .map(|tracked| (*uuid, tracked.position))
            })
            .collect();
        (listener, players)
    }
}
