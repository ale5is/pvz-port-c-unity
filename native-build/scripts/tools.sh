#!/usr/bin/env bash
set -euo pipefail
root=$HOME/modbuild
MIXINS_REV=a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab
SDK=/usr/local/lib/android/sdk
if printenv ANDROID_HOME >/dev/null 2>&1; then SDK=$(printenv ANDROID_HOME); fi
sudo apt-get update
sudo apt-get install -y curl git python3 rsync unzip zip pkg-config libasound2-dev libudev-dev libxkbcommon-dev libwayland-dev libx11-dev libxi-dev libxrandr-dev libxcursor-dev libxinerama-dev libxfixes-dev libxext-dev util-linux
mkdir -p "$root/cargo" "$root/base" "$HOME/.rustup"
export CARGO_HOME="$root/cargo"
export RUSTUP_HOME="$HOME/.rustup"
if [ ! -x "$CARGO_HOME/bin/cargo" ]; then
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain 1.95.0 --profile minimal --target aarch64-linux-android
fi
. "$CARGO_HOME/env"
rustup toolchain install 1.95.0 --profile minimal --target aarch64-linux-android
rustup default 1.95.0
export ANDROID_HOME="$SDK"
export ANDROID_SDK_ROOT="$SDK"
SDKMANAGER=$(find "$SDK/cmdline-tools" -type f -name sdkmanager | head -n 1)
[ -n "$SDKMANAGER" ] || { echo "Android sdkmanager not found under $SDK" >&2; exit 1; }
yes | "$SDKMANAGER" --licenses >/dev/null
"$SDKMANAGER" "ndk;27.2.12479018" "platforms;android-35" "build-tools;35.0.0"
ln -sfn "$SDK" "$HOME/android"
tool="$root/base/mixins-$MIXINS_REV"
if [ ! -x "$tool/bin/horizon-mixins" ]; then
  cargo install --locked --git https://github.com/horizon-rust/horizon-mixins.git --rev "$MIXINS_REV" --root "$tool" horizon-mixins-cli
fi
cargo fetch --locked --manifest-path "$root/source/Cargo.toml"
echo "Rust, Android NDK and horizon-mixins are ready."
