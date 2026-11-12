# SVC client crate

This crate contains the protocol, UDP session, Opus codec bindings, and CPAL device adapters used by the Horizon mod.

Host validation:

```sh
cargo test --manifest-path mod/crates/svc_client/Cargo.toml --all-targets
cargo fmt --manifest-path mod/crates/svc_client/Cargo.toml -- --check
```

Linux builds require `pkg-config` and the ALSA development headers. Android builds must use the NDK toolchain selected by the Horizon mod build environment; a successful host test does not establish Android device compatibility.
