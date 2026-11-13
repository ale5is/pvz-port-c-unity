#!/usr/bin/env bash
set -euo pipefail
version=0.7.8
if [ "$#" -gt 0 ]; then version=$1; fi
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
root=$HOME/modbuild
commit=$(python3 - "$repo_root/native-build/versions.json" "$version" <<'PY'
import json, sys
versions = json.load(open(sys.argv[1]))
try:
    print(versions[sys.argv[2]])
except KeyError:
    raise SystemExit("unsupported Horizon version: " + sys.argv[2])
PY
)
mkdir -p "$root" "$root/cargo" "$root/base" "$root/jobs" "$root/patches/horizon-078" "$root/builtin"
if [ ! -d "$root/source/.git" ]; then
  rm -rf "$root/source"
  git init "$root/source"
  git -C "$root/source" remote add origin https://github.com/starling-client/starling.git
fi
git -C "$root/source" fetch --depth 1 origin "$commit"
git -C "$root/source" checkout -q --detach FETCH_HEAD
[ "$(git -C "$root/source" rev-parse HEAD)" = "$commit" ]
cp "$repo_root/patches/horizon-078/audio-permission.patch" "$root/patches/horizon-078/"
cp "$repo_root/patches/horizon-078/voicechat-packets.patch" "$root/patches/horizon-078/"
rm -rf "$root/builtin/simple-voice-chat"
cp -r "$repo_root/mod/simple-voice-chat" "$root/builtin/simple-voice-chat"
echo "$commit" > "$root/HORIZON_COMMIT"
echo "$version" > "$root/HORIZON_VERSION"
echo "Prepared Horizon $version at $commit in $root"
