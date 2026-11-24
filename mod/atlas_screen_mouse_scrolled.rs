fn atlas_screen_mouse_scrolled(
    game: &crate::game::Game<'_>,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
    scroll: [f64; 2],
    _modifiers: u32,
    canceled: &mut bool,
) {
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }
    crate::game::atlas_mod::with(|state| {
        if !matches!(&state.mode, crate::game::atlas_mod::Mode::Closed) {
            state.scroll_by(scroll[1], screen.mouse);
        }
    });
    *canceled = true;
}
