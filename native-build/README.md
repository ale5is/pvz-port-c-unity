# Horizon 0.7.8 native mod builder

This ports the Starlight/Starling remote native-mod build workflow to the pinned Horizon 0.7.8
source tree. The interface lives inside Horizon, but compilation runs on a trusted GitHub Actions
runner, not on the Android device.

## Exact compatibility target

The builder accepts only native `.hnmod` source bundles matching all of these pins:

- Horizon 0.7.8, source commit `68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29`
- Minecraft 26.3
- `horizon-mixins` commit `a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab`
- Horizon native hooks API 80
- Android `arm64-v8a`

Starling 0.8.0's hooks API 359 is not binary-compatible with this Horizon target and is rejected.
Regular Fabric `.jar` mods and content `.hmod` files are not converted into native Rust mods.

## In-game workflow

1. Import a compatible `.hnmod` package with Horizon's Android file picker. It is stored in
   `files/horizon-native-mods/`.
2. Put the gateway's public HTTPS base URL, without a trailing slash, in
   `files/horizon-native-mods/build-server.txt`.
3. Open **Options → Mods → Native Mod Builder** and choose **Compile installed native mods**.
4. Horizon uploads the packages, shows the build status, verifies the returned SHA-256 and build
   metadata, then stores the ARM64 library under `files/native/`.
5. Restart Horizon to load the compiled library.

The second button, **Revert to original Horizon library on next launch**, removes the active-build
pointer. The Android bootstrap also falls back to the APK's original library if a custom build
cannot remain stable for 45 seconds on two consecutive launches.

## Performance boundaries

- The builder has only `mods`, `settings` and `settings_click` hooks; it adds no gameplay frame hook.
- Package collection, upload, status polling, download and SHA-256 verification run on a worker
  thread after the player presses Compile.
- Rust compilation happens on GitHub Actions, never in the game's process.
- The custom library is loaded once at app startup; no runtime hot reload is attempted.
- The Android build uses Horizon's `fast` optimized profile.

The structural checks enforce these boundaries. A device-side FPS/RAM benchmark is still required
before claiming a measured performance delta.

## Build service

The gateway keeps the GitHub token off the Android device. It validates the `.hnmod` packages,
dispatches `horizon-mod-build.yml`, polls the run, checks the artifact SHA-256 and serves the
compiled library.

Build the Docker image from the repository root:

```sh
docker build -f native-build/gateway/Dockerfile -t horizon-mod-build .
```

Run it behind an HTTPS reverse proxy:

```sh
docker run -d --restart unless-stopped -p 127.0.0.1:8080:8080 \
  -v /var/lib/horizon-mod-build:/data \
  -e GITHUB_TOKEN=YOUR_SERVER_SIDE_TOKEN \
  -e GITHUB_REPOSITORY=ale5is/pvz-port-c-unity \
  -e GITHUB_REF=port/horizon-078-hooks-359 \
  -e PUBLIC_BASE_URL=https://build.example.com \
  horizon-mod-build
```

The fine-grained GitHub token needs Actions read/write access to this repository. Do not put it in
the APK or in `build-server.txt`. Do not expose port 8080 directly to the internet; terminate TLS
at a reverse proxy, enforce request-size limits and rate limits there too. The default gateway
limits each source IP to three requests per hour and allows three concurrent jobs.

## Security

The GitHub worker validates archive paths, bundle IDs, compatibility pins and size limits. It
compiles untrusted mod source in a networkless sandbox without runner secrets. The resulting native
mods still execute with the game's process permissions, so only compile packages from authors you
trust.
