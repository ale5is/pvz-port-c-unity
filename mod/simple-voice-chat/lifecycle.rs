fn world_lifecycle(
    game: &mut crate::game::Game<'_>,
    context: &crate::game::native_lifecycle::NativeLifecycleContext,
    phase: crate::game::native_lifecycle::WorldPhase,
    error_text: &str,
) {
    crate::game::simple_voice_chat::world_lifecycle(game, context, phase, error_text);
}
