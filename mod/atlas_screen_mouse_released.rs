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
    let captured = crate::game::atlas_mod::with(|state| {
        let captured = state.pointer_buttons.remove(&button);
        if captured {
            state.release(button);
        }
        captured
    });
    if captured {
        *canceled = true;
    }
}
