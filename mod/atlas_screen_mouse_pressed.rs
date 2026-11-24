fn atlas_screen_mouse_pressed(
    game: &crate::game::Game<'_>,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
    button: i32,
    _modifiers: u32,
    canceled: &mut bool,
) {
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }
    let here = crate::game::atlas_bridge::here(game);
    let size = [screen.rect[2], screen.rect[3]];
    let measure = |text: &str| game.native_text_width(text);
    crate::game::atlas_mod::with(|state| {
        if matches!(&state.mode, crate::game::atlas_mod::Mode::Closed) {
            return;
        }
        let (_, widgets) = state.screen(size, screen.mouse, here.as_ref(), &measure);
        state.press(button, screen.mouse, &widgets, here.as_ref());
    });
    *canceled = true;
}
