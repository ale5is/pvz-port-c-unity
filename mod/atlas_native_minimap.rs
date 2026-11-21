fn atlas_native_minimap(
    game: &crate::game::Game<'_>,
    minimap: &mut Option<crate::game::native_hooks::NativeMinimap>,
) {
    if minimap.is_some() || !game.native_on("atlas") {
        return;
    }

    let (enlarged, rotate, blocks_per_pixel, round) =
        crate::game::atlas_mod::with(|state| {
            (
                state.enlarged,
                state.turning(),
                state.mini_zoom(),
                state.settings().round,
            )
        });

    *minimap = Some(crate::game::native_hooks::NativeMinimap {
        corner: horizon_client::extras::Corner::TopRight,
        size: if enlarged { 160.0 } else { 96.0 },
        blocks_per_pixel,
        rotate,
        round,
    });
}
