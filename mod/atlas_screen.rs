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
    atlas_report_text_caret(game, size);
}


fn atlas_report_text_caret(game: &crate::game::Game<'_>, size: [f32; 2]) {
    let field = crate::game::atlas_mod::with(|state| match &state.mode {
        crate::game::atlas_mod::Mode::Edit(editor) => {
            let pw = (size[0] - 16.0).min(250.0);
            let ph = 178.0_f32.min(size[1] - 8.0);
            let px = ((size[0] - pw) / 2.0).round();
            let py = ((size[1] - ph) / 2.0).round().max(4.0);
            let left = px + 6.0;
            let inner = pw - 12.0;
            match editor.focus {
                crate::game::atlas_mod::Field::Name => {
                    Some((editor.name.clone(), left + 3.0, py + 33.0, inner - 6.0))
                }
                crate::game::atlas_mod::Field::X
                | crate::game::atlas_mod::Field::Y
                | crate::game::atlas_mod::Field::Z => {
                    let index = match editor.focus {
                        crate::game::atlas_mod::Field::X => 0,
                        crate::game::atlas_mod::Field::Y => 1,
                        crate::game::atlas_mod::Field::Z => 2,
                        crate::game::atlas_mod::Field::Name => unreachable!(),
                    };
                    let third = (inner - 8.0) / 3.0;
                    let x = left + index as f32 * (third + 4.0);
                    Some((editor.coords[index].clone(), x + 3.0, py + 61.0, third - 6.0))
                }
            }
        }
        crate::game::atlas_mod::Mode::List if state.filter_focus => {
            let width = (size[0] - 16.0).min(400.0).floor();
            let left = ((size[0] - width) / 2.0).floor();
            let field_width = 104.0_f32.min(width);
            Some((state.list_filter.clone(), left + 3.0, 54.0, field_width - 6.0))
        }
        crate::game::atlas_mod::Mode::Settings(view) if view.searching => {
            let gap = 8.0;
            let bw = ((size[0] - 24.0 - gap) / 2.0).min(200.0).floor();
            let left = ((size[0] - bw * 2.0 - gap) / 2.0).floor();
            Some((view.search.clone(), left + bw + gap + 4.0, 26.0, bw - 8.0))
        }
        _ => None,
    });

    if let Some((text, x, y, max_width)) = field {
        let width = game.native_text_width(&text).min(max_width.max(1.0));
        horizon_client::preedit::report_caret(x + width, y, 10.0);
    }
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
