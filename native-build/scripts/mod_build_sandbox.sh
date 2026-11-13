#!/usr/bin/env bash
# Runs the build in a networkless mount namespace with a disposable overlay cache.
set -euo pipefail
root=$1
commit=$2
job=$3
script_dir=$(cd "$(dirname "$0")" && pwd)
MOD_BUILD_SECONDS=3200
MOD_BUILD_BASE=0
if [ -n "${MOD_BUILD_SECONDS_OVERRIDE-}" ]; then MOD_BUILD_SECONDS=$MOD_BUILD_SECONDS_OVERRIDE; fi
if [ -n "${MOD_BUILD_BASE_OVERRIDE-}" ]; then MOD_BUILD_BASE=$MOD_BUILD_BASE_OVERRIDE; fi
git -C "$root/source" checkout -q --detach "$commit"
cleanup() { if [ "$MOD_BUILD_BASE" != 1 ]; then sudo -n rm -rf "$job/overlay" "$job/rustup-overlay"; fi; }
trap cleanup EXIT
sudo -n timeout --kill-after=10 "$MOD_BUILD_SECONDS" \
  unshare --net --mount --pid --fork --kill-child --propagation private \
  bash "$script_dir/sandbox_inner.sh" "$HOME" "$root" "$job" \
  "$(id -u)" "$(id -g)" "$MOD_BUILD_BASE" "$script_dir" "$JAVA_HOME"
