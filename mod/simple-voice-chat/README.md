# Simple Voice Chat — Horizon 0.7.8 port

This is the API-80 adaptation workspace. It keeps the upstream protocol/audio crate and replaces hook bindings that do not exist in Horizon 0.7.8.

- Target Horizon: `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`
- Native hooks API: `80`
- horizon-mixins: `a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab`

The obsolete `player_submit` injection was removed because it is not in API 80 and its implementation was only a placeholder. The newer `input_key` event was replaced by API-80 `key`, which passes the vanilla key name and pressed state. The lifecycle adapter uses API-80's context, by-value phase, and error text.

Build this folder with the 0.7.8 launcher's native-mod build command after the crate sources and Opus vendor tree are complete. This workspace has not yet been compiled or tested.
