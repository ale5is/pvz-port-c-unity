impl super::Remote {
    pub(super) fn voice_player_positions(&self) -> Vec<(String, [f64; 3])> {
        self.level_players
            .iter()
            .filter_map(|(entity_id, uuid)| {
                if *uuid == self.profile {
                    return None;
                }
                let name = self.level_player_name(*entity_id, uuid)?;
                let (kind, position) = self.tracked(*entity_id)?;
                if kind != "minecraft:player" {
                    return None;
                }
                Some((name.to_owned(), position))
            })
            .collect()
    }
}
