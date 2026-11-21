fn atlas_native_minimap(
    game: &crate::game::Game<'_>,
    minimap: &mut Option<crate::game::native_hooks::NativeMinimap>,
) {
    if minimap.is_some() || !game.native_on("atlas") {
        return;
    }

    let enlarged = crate::game::atlas_mod::with(|state| state.enlarged);
    *minimap = Some(crate::game::native_hooks::NativeMinimap {
        corner: horizon_client::extras::Corner::TopRight,
        size: if enlarged { 160.0 } else { 96.0 },
        blocks_per_pixel: 1.0,
        rotate: true,
        round: false,
    });
}
