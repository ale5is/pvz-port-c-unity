#!/usr/bin/env bash
set -euo pipefail
df -h /
rm -rf "$HOME/.gradle/caches" "$HOME/.cache/pip" "$HOME/.npm" 2>/dev/null || true
df -h /
