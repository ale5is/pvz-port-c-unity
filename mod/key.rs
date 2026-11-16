fn svc_key_hook(key_name: &str, pressed: bool) {
    crate::game::simple_voice_chat::on_key(key_name, pressed);
}
