fn atlas_screen_render(
    game: &crate::game::Game<'_>,
    batch: &mut horizon_client::hud::HudBatch,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
) {
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }
    let here = crate::game::atlas_bridge::here(game);
    let size = [screen.rect[2], screen.rect[3]];
    let measure = |text: &str| game.native_text_width(text);
    let (draws, _) = crate::game::atlas_mod::with(|state| {
        if matches!(&state.mode, crate::game::atlas_mod::Mode::Closed) {
            return (Vec::new(), Vec::new());
        }
        state.screen(size, screen.mouse, here.as_ref(), &measure)
    });
    crate::game::atlas_bridge::paint(game, batch, draws);
}


fn atlas_screen_char_typed(
    game: &crate::game::Game<'_>,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
    c: char,
    modifiers: u32,
    canceled: &mut bool,
) {
    let _ = modifiers;
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }

    let focused = crate::game::atlas_mod::with(|state| match &state.mode {
        crate::game::atlas_mod::Mode::Edit(_) => true,
        crate::game::atlas_mod::Mode::List => state.filter_focus,
        crate::game::atlas_mod::Mode::Settings(view) => view.searching,
        _ => false,
    });
    if !focused {
        return;
    }

    crate::game::atlas_mod::with(|state| state.char_typed(c));
    *canceled = true;
}
