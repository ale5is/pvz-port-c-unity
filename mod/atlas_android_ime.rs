pub(super) fn poll(game: &mut crate::game::Game<'_>) {
    if !game.native_on(crate::game::atlas_mod::ID)
        || game.screen != crate::game::Screen::Paused
    {
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

    for output in crate::platform::poll() {
        match output {
            horizon_client::osk::Output::Text(text) => {
                game.screen_chars_typed(Some(&text));
            }
            horizon_client::osk::Output::Backspace => {
                game.screen_key_pressed(winit::keyboard::KeyCode::Backspace, false);
            }
            horizon_client::osk::Output::Enter => {
                game.screen_key_pressed(winit::keyboard::KeyCode::Enter, false);
            }
            horizon_client::osk::Output::Close => crate::platform::hide(),
            horizon_client::osk::Output::Key(code) => {
                game.screen_key_pressed(code, false);
            }
        }
    }

    let still_focused = crate::game::atlas_mod::with(|state| match &state.mode {
        crate::game::atlas_mod::Mode::Edit(_) => true,
        crate::game::atlas_mod::Mode::List => state.filter_focus,
        crate::game::atlas_mod::Mode::Settings(view) => view.searching,
        _ => false,
    });
    if !still_focused {
        crate::platform::hide();
    }
}
