# Simple Voice Chat — Horizon Nightly native adapter

This is the first isolated port of the integration boundary to Horizon's current
native mod API. It intentionally does not replace the existing Android launcher,
the legacy mod bundle, or the Opus/UDP/audio implementation.

## Implemented in this stage

- Uses the current `horizon-mod.toml` + `Cargo.toml` native package layout.
- Pins the exact Horizon Nightly and `horizon-mixins` revisions used for API builds.
- Declares the Simple Voice Chat raw plugin channels without adding a custom
  negotiation envelope, preserving compatibility with vanilla-shaped SVC servers.
- Receives server play custom payloads through `Hooks::client_packet`.
- Registers PTT and the three mute controls through the current key-mapping registrar.
- Exposes bounded queues through `take_incoming()` and `take_controls()` for the
  existing `svc_client` runtime.

## Not yet implemented

This adapter is an API boundary, not a complete working voice mod. The next
integration stage must connect these queues to `svc_client::runtime::Glue`,
drive its world/session lifecycle, send its pending custom payloads through
`ClientHost::send_payload_to_server`, restore local/remote player position
updates, surface command replies and HUD state, and bridge Android microphone
permission. Until those are implemented and tested, this package must not be
installed as the playable SVC mod.

The legacy `horizon-mixin.toml` adapter remains untouched as a reference while
the new native package is validated independently.
