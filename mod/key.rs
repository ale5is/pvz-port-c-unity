fn svc_key(game: &crate::game::Game<'_>, key: &str, pressed: bool) {
    crate::game::simple_voice_chat::on_key(game, key, pressed);
    crate::game::jade::on_key(game, key, pressed);
}

    crate::game::atlas_mod::bridge::key_api80(game, key, pressed);
