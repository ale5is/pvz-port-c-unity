fn atlas_native_minimap(
    game: &crate::game::Game<'_>,
    minimap: &mut Option<crate::game::native_hooks::NativeMinimap>,
) {
    if minimap.is_some() || !game.native_on("atlas") {
        return;
    }

    let render_distance = game.settings.render_distance as i32;
    let (enlarged, rotate, blocks_per_pixel, round) =
        crate::game::atlas_mod::with(|state| {
            let rotate = state.turning();
            (
                state.enlarged,
                rotate,
                state.mini_zoom_for_render_distance(render_distance, rotate),
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
