#!/usr/bin/env python3
"""Strict .hnmod request decoder shared by the Horizon build gateway and GitHub worker."""
from __future__ import annotations
import io
import json
import pathlib
import re
import tomllib
import zipfile

MAX_BODY = 32 << 20
MAX_MODS = 16
MAX_ENTRIES = 512
MAX_EXPANDED = 64 << 20
MAX_FILE = 64 << 20
ID = re.compile(r"^[a-z0-9_]{1,64}$")
REV = re.compile(r"^[0-9a-f]{40}$")


def allowed(path: str) -> bool:
    parts = path.split("/")
    name = parts[-1]
    in_data = len(parts) >= 2 and (
        parts[0] == "data" or (len(parts) >= 3 and parts[1] == "data")
    )
    text = len(parts) <= 2 and (
        name.startswith(("README", "LICENSE", "LICENCE", "COPYING", "NOTICE"))
        or name == ".gitignore"
    )
    return path.endswith((".rs", ".toml")) or in_data or text


def decode_bundle(data: bytes) -> tuple[str, dict[str, bytes], dict]:
    try:
        archive = zipfile.ZipFile(io.BytesIO(data))
    except Exception as error:
        raise ValueError(f"invalid .hnmod ZIP: {error}") from error
    if len(archive.infolist()) > MAX_ENTRIES:
        raise ValueError("native bundle has too many entries")
    files: dict[str, bytes] = {}
    total = 0
    for item in archive.infolist():
        name = item.filename
        if item.is_dir():
            continue
        mode = (item.external_attr >> 16) & 0o170000
        if (
            name.startswith("/")
            or "\\" in name
            or ":" in name
            or any(part in ("", ".", "..") for part in name.split("/"))
            or (mode not in (0, 0o100000))
        ):
            raise ValueError(f"invalid native bundle path: {name}")
        if name in files:
            raise ValueError("duplicate native bundle path")
        if item.file_size > MAX_FILE:
            raise ValueError("native bundle file exceeds 16 MiB")
        payload = archive.read(item)
        total += len(payload)
        if total > MAX_EXPANDED:
            raise ValueError("native bundle expands beyond 64 MiB")
        files[name] = payload
    try:
        manifest = json.loads(files["horizon-native.json"])
    except Exception as error:
        raise ValueError(f"invalid horizon-native.json: {error}") from error
    mod_id = manifest.get("id", "")
    if (
        manifest.get("schema") != 1
        or not ID.fullmatch(mod_id)
        or not manifest.get("version")
        or not manifest.get("game")
        or not REV.fullmatch(manifest.get("horizon", ""))
        or not REV.fullmatch(manifest.get("mixins", ""))
        or not isinstance(manifest.get("hooks", 0), int)
        or isinstance(manifest.get("hooks", 0), bool)
        or manifest.get("hooks", 0) < 0
    ):
        raise ValueError("invalid native manifest: schema, id, version, game and revisions required")
    prefix = f"mixins/{mod_id}/"
    for name in files:
        if name == "horizon-native.json":
            continue
        rest = name.removeprefix(prefix)
        if rest == name or not allowed(rest):
            raise ValueError(f"unexpected native source: {name}")
    mixin_path = prefix + "horizon-mixin.toml"
    if mixin_path not in files:
        raise ValueError("missing horizon-mixin.toml")
    try:
        mixin = tomllib.loads(files[mixin_path].decode("utf-8"))
    except Exception as error:
        raise ValueError(f"invalid horizon-mixin.toml: {error}") from error
    compatibility = mixin.get("compatibility", {})
    mod = mixin.get("mod", {})
    if (
        mod.get("id") != mod_id
        or mod.get("version") != manifest["version"]
        or compatibility.get("horizon_revision") != manifest["horizon"]
    ):
        raise ValueError("native manifest and mixin identity/revision differ")
    return mod_id, files, manifest


def unpack_request(body: bytes, destination: pathlib.Path) -> list[dict]:
    if len(body) > MAX_BODY:
        raise ValueError("request exceeds 32 MiB")
    destination.mkdir(parents=True, exist_ok=True)
    offset = 0
    seen = set()
    manifests = []
    while offset < len(body):
        if len(body) - offset < 4:
            raise ValueError("truncated bundle length")
        length = int.from_bytes(body[offset:offset + 4], "big")
        offset += 4
        if length <= 0 or length > len(body) - offset:
            raise ValueError("truncated bundle payload")
        mod_id, files, manifest = decode_bundle(body[offset:offset + length])
        offset += length
        if mod_id in seen:
            raise ValueError(f"duplicate native mod id: {mod_id}")
        seen.add(mod_id)
        if len(seen) > MAX_MODS:
            raise ValueError("too many native mods in one build")
        folder = destination / mod_id
        prefix = f"mixins/{mod_id}/"
        for name, payload in files.items():
            if name == "horizon-native.json":
                continue
            relative = name.removeprefix(prefix)
            target = folder / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(payload)
        manifests.append(manifest)
    if not manifests:
        raise ValueError("request contains no native mods")
    return manifests
