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
                crate::game::atlas_mod::with(|state| {
                    for character in text.chars() {
                        state.char_typed(character);
                    }
                });
            }
            horizon_client::osk::Output::Backspace => {
                crate::game::atlas_mod::with(|state| {
                    state.key("key.keyboard.backspace", false, None, ["", "", "", ""]);
                });
            }
            horizon_client::osk::Output::Enter => {
                crate::game::atlas_mod::with(|state| {
                    state.key("key.keyboard.enter", false, None, ["", "", "", ""]);
                });
            }
            horizon_client::osk::Output::Close => crate::platform::hide(),
            horizon_client::osk::Output::Key(_) => {}
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
