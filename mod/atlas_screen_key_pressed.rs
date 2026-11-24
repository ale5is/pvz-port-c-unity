fn atlas_screen_key_pressed(
    game: &crate::game::Game<'_>,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
    key: &str,
    repeat: bool,
    modifiers: u32,
    canceled: &mut bool,
) {
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }
    let map = game.native_mod_key("atlas", crate::game::atlas_mod::KEY_MAP, crate::game::atlas_mod::DEFAULT_MAP);
    let create = game.native_mod_key("atlas", crate::game::atlas_mod::KEY_CREATE, crate::game::atlas_mod::DEFAULT_CREATE);
    let list = game.native_mod_key("atlas", crate::game::atlas_mod::KEY_LIST, crate::game::atlas_mod::DEFAULT_LIST);
    let settings = game.native_mod_key("atlas", crate::game::atlas_mod::KEY_SETTINGS, crate::game::atlas_mod::DEFAULT_SETTINGS);
    let here = crate::game::atlas_bridge::here(game);
    crate::game::atlas_mod::with(|state| {
        if matches!(&state.mode, crate::game::atlas_mod::Mode::Closed) {
            return;
        }
        let typing = crate::game::atlas_mod::key_char(key).is_some()
            && match &state.mode {
                crate::game::atlas_mod::Mode::Edit(_) => true,
                crate::game::atlas_mod::Mode::List => state.filter_focus,
                crate::game::atlas_mod::Mode::Settings(view) => view.searching,
                _ => false,
            };
        if typing {
            return;
        }
        if !repeat {
            state.key(
                key,
                modifiers & 1 != 0,
                here.as_ref(),
                [&map, &create, &list, &settings],
            );
        }
        *canceled = true;
    });
}
