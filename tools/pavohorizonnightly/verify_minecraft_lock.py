#!/usr/bin/env python3
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.request import urlopen

LOCK = Path(__file__).with_name("minecraft-26.3-lock.json")
HEX40 = re.compile(r"^[0-9a-f]{40}$")
HEX64 = re.compile(r"^[0-9a-f]{64}$")

def require(condition, message):
    if not condition:
        raise SystemExit(message)

data = json.loads(LOCK.read_text(encoding="utf-8"))
require(data.get("schema") == 1, "Unsupported Minecraft lock schema")
mc = data["minecraft"]
runtime = data["android_runtime"]
require(mc["version"] == "26.3", "Unexpected Minecraft version")
require(mc["java_major"] == 25, "Minecraft 26.3 preparation requires Java 25")
require(HEX40.fullmatch(mc["manifest"]["sha1"]) is not None, "Invalid manifest SHA-1")
require(all(HEX40.fullmatch(x) for x in mc["manifest"]["accepted_sha1"]), "Invalid accepted manifest SHA-1")
require(HEX40.fullmatch(mc["client"]["sha1"]) is not None, "Invalid client SHA-1")
require(HEX40.fullmatch(mc["server"]["sha1"]) is not None, "Invalid server SHA-1")
require(runtime["abi"] == "arm64-v8a", "Unexpected Android ABI")
require(HEX64.fullmatch(runtime["sha256"]) is not None, "Invalid Java runtime SHA-256")
require(runtime["size"] > 0, "Invalid Java runtime size")
require("piston-meta.mojang.com" in mc["manifest"]["url"], "Manifest must come from Mojang metadata")
require("github.com/AngelAuraMC/" in runtime["url"], "Unexpected Java runtime publisher")
if "--network" in sys.argv:
    with urlopen(mc["manifest"]["url"], timeout=30) as response:
        payload = response.read()
    actual = hashlib.sha1(payload).hexdigest()
    accepted = {mc["manifest"]["sha1"], *mc["manifest"]["accepted_sha1"]}
    require(actual in accepted, f"Mojang manifest SHA-1 mismatch: {actual}")
    manifest = json.loads(payload)
    require(manifest.get("id") == mc["version"], "Mojang manifest version ID mismatch")
    require(manifest.get("downloads", {}).get("client", {}).get("sha1") == mc["client"]["sha1"], "Client SHA-1 differs from pinned lock")
    require(manifest.get("downloads", {}).get("server", {}).get("sha1") == mc["server"]["sha1"], "Server SHA-1 differs from pinned lock")
print("Minecraft 26.3 lock: schema and pinned hashes valid")
if "--network" in sys.argv:
    print("Mojang manifest: live SHA-1, version ID, client and server hashes verified")
