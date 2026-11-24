fn atlas_screen_mouse_released(
    game: &crate::game::Game<'_>,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
    button: i32,
    _modifiers: u32,
    canceled: &mut bool,
) {
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }
    crate::game::atlas_mod::with(|state| {
        if !matches!(&state.mode, crate::game::atlas_mod::Mode::Closed) {
            state.release(button);
        }
    });
    *canceled = true;
}
