#!/usr/bin/env bash
# Build Horizon 0.7.8 for Android ARM64 with Simple Voice Chat and user native bundles.
set -euo pipefail
MIXINS_REV=a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab
NDK_VERSION=27.2.12479018
BUILD_TOOLS_VERSION=35.0.0
ABI=arm64-v8a
PROFILE=fast
HORIZON_VERSION=0.7.8
GAME_VERSION=26.3
HOOKS_API=80
NATIVE_CACHE=$HOME/.cache/horizon-native
export NATIVE_CACHE
export CARGO_HOME="$HOME/.cargo"
export RUSTUP_HOME="$HOME/.rustup"
TOOLCHAIN_BIN="$RUSTUP_HOME/toolchains/1.95.0-x86_64-unknown-linux-gnu/bin"
export PATH="$TOOLCHAIN_BIN:$CARGO_HOME/bin:/usr/bin:/bin"
export RUSTC="$TOOLCHAIN_BIN/rustc"
command -v cargo >/dev/null || { echo "native_build: cargo is missing from the isolated toolchain" >&2; exit 1; }
fail() { echo "native_build: $*" >&2; exit 1; }
[ "$#" -eq 3 ] || fail "usage: native_build.sh SOURCE OUT MODS"
src=$(cd "$1" && pwd)
mkdir -p "$2"
out=$(cd "$2" && pwd)
mods_in=$(cd "$3" && pwd)
mkdir -p "$NATIVE_CACHE"
commit=$(git -C "$src" rev-parse HEAD)
expected=68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29
[ "$commit" = "$expected" ] || fail "unsupported Horizon source commit: $commit"
tool=$NATIVE_CACHE/mixins-$MIXINS_REV/bin/horizon-mixins
[ -x "$tool" ] || fail "horizon-mixins is missing from the clean build cache"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir "$work/source"
git -C "$src" archive --format=tar HEAD | tar -x -C "$work/source"
find "$work/source" -type l -delete
echo "$commit" > "$work/source/.horizon-revision"
git -C "$work/source" apply "$HOME/patches/horizon-078/audio-permission.patch"
git -C "$work/source" apply "$HOME/patches/horizon-078/voicechat-packets.patch"
python3 "$HOME/patches/horizon-078/native-mod-system.py" "$work/source"
mkdir "$work/mods"
cp -r "$HOME/builtin/simple-voice-chat" "$work/mods/simple-voice-chat"
cp -r "$HOME/builtin/native-mod-builder" "$work/mods/horizon-native-builder"
for bundle in "$mods_in"/*/; do
  [ -d "$bundle" ] || continue
  id=$(basename "$bundle")
  case "$id" in simple-voice-chat|native-mod-builder|horizon-native-builder) fail "$id is a reserved built-in bundle id" ;; esac
  [ -f "$bundle/horizon-mixin.toml" ] || fail "$id has no horizon-mixin.toml"
  [ ! -e "$work/mods/$id" ] || fail "duplicate mod id: $id"
  cp -r "$bundle" "$work/mods/$id"
done
python3 - "$work/mods" "$commit" <<'PY'
import pathlib, sys, tomllib
mods, commit = pathlib.Path(sys.argv[1]), sys.argv[2]
for folder in sorted(mods.iterdir()):
    manifest = folder / "horizon-mixin.toml"
    if not manifest.is_file():
        raise SystemExit(f"{folder.name}: missing horizon-mixin.toml")
    value = tomllib.loads(manifest.read_text())
    mod = value.get("mod", {})
    if mod.get("id") != folder.name:
        raise SystemExit(f"{folder.name}: directory and [mod].id differ")
    pin = value.get("compatibility", {}).get("horizon_revision")
    if pin != commit:
        raise SystemExit(f"{folder.name}: targets Horizon {pin!r}, build source is {commit}")
PY
echo "native_build: applying native mod bundles"
"$tool" apply --workspace "$work/source" --mods "$work/mods" --out "$work/stage"
build=$NATIVE_CACHE/build-$ABI
mkdir -p "$build"
rsync -r --checksum --delete --exclude /target/ "$work/stage/" "$build/"
cargo fetch --locked --manifest-path "$build/Cargo.toml"
ANDROID_HOME=$HOME/android
export ANDROID_HOME
(cd "$build" && python3 tools/android_build.py \
  --abi "$ABI" --ndk-version "$NDK_VERSION" \
  --build-tools-version "$BUILD_TOOLS_VERSION" \
  --sdk "$ANDROID_HOME" --java-home "$JAVA_HOME" --profile "$PROFILE" --rust-only)
library=$build/target/aarch64-linux-android/$PROFILE/libhorizon_android.so
[ -s "$library" ] || fail "Android ARM64 library was not produced: $library"
cp "$library" "$out/libhorizon_android.so"
python3 - "$work/mods" "$out" "$commit" "$MIXINS_REV" "$ABI" "$PROFILE" "$HORIZON_VERSION" "$GAME_VERSION" "$HOOKS_API" <<'PY'
import hashlib, json, pathlib, sys, tomllib
mods, out, commit, mixins, abi, profile, horizon_version, game_version, hooks_api = sys.argv[1:]
mods, out = pathlib.Path(mods), pathlib.Path(out)
entries = []
for folder in sorted(p for p in mods.iterdir() if p.is_dir()):
    value = tomllib.loads((folder / "horizon-mixin.toml").read_text())
    entries.append({"id": value["mod"]["id"], "version": value["mod"]["version"]})
library = out / "libhorizon_android.so"
json.dump({
    "schema": 1, "version": horizon_version, "game": game_version, "hooks": int(hooks_api), "commit": commit,
    "horizon_mixins": mixins, "abi": abi, "profile": profile,
    "mods": entries,
    "sha256": hashlib.sha256(library.read_bytes()).hexdigest(),
    "size": library.stat().st_size,
}, (out / "build.json").open("w"), indent=2)
PY
cat "$out/build.json"
