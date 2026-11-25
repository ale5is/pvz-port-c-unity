fn atlas_screen_mouse_pressed(
    game: &crate::game::Game<'_>,
    screen: &crate::game::native_hooks::NativeScreenView<'_>,
    button: i32,
    modifiers: u32,
    canceled: &mut bool,
) {
    let _ = modifiers;
    if !game.native_on("atlas") || screen.id != "paused" {
        return;
    }
    let here = crate::game::atlas_bridge::here(game);
    let size = [screen.rect[2], screen.rect[3]];
    let measure = |text: &str| game.native_text_width(text);
    let (captured, keyboard_text, hide_keyboard) = crate::game::atlas_mod::with(|state| {
        if matches!(&state.mode, crate::game::atlas_mod::Mode::Closed) {
            return (false, None, false);
        }
        let (_, widgets) = state.screen(size, screen.mouse, here.as_ref(), &measure);
        let focused = match crate::game::atlas_mod::hit(&widgets, screen.mouse) {
            Some(crate::game::atlas_mod::Action::Focus(field)) if button == 0 => Some(field),
            _ => None,
        };
        let editing = matches!(&state.mode, crate::game::atlas_mod::Mode::Edit(_));
        state.press(button, screen.mouse, &widgets, here.as_ref());
        state.pointer_buttons.insert(button);
        let text = focused.and_then(|field| {
            let crate::game::atlas_mod::Mode::Edit(editor) = &state.mode else {
                return None;
            };
            Some(match field {
                crate::game::atlas_mod::Field::Name => editor.name.clone(),
                crate::game::atlas_mod::Field::X => editor.coords[0].clone(),
                crate::game::atlas_mod::Field::Y => editor.coords[1].clone(),
                crate::game::atlas_mod::Field::Z => editor.coords[2].clone(),
            })
        });
        (true, text, editing && focused.is_none())
    });
    #[cfg(target_os = "android")]
    {
        if let Some(text) = keyboard_text.as_deref() {
            crate::platform::show(text);
        } else if hide_keyboard {
            crate::platform::hide();
        }
    }
    #[cfg(not(target_os = "android"))]
    let _ = (keyboard_text, hide_keyboard);
    if captured {
        *canceled = true;
    }
}
