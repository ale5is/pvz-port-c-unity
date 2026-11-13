#!/usr/bin/env python3
"""Small HTTPS-facing gateway for Horizon's GitHub Actions native-mod build worker.

Keep GITHUB_TOKEN on this server only. The Android client receives only random request ids.
"""
from __future__ import annotations
import hashlib
import http.server
import io
import json
import os
import pathlib
import re
import secrets
import shutil
import tempfile
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent
sys.path.insert(0, str(ROOT / "scripts"))
from mod_build_server import check_compatibility, unpack_request

TOKEN = os.environ.get("GITHUB_TOKEN", "")
REPOSITORY = os.environ.get("GITHUB_REPOSITORY", "ale5is/pvz-port-c-unity")
REF = os.environ.get("GITHUB_REF", "port/horizon-078-hooks-359")
WORKFLOW = os.environ.get("GITHUB_WORKFLOW", "horizon-mod-build.yml")
PUBLIC_URL = os.environ.get("PUBLIC_BASE_URL", "").rstrip("/")
DATA = pathlib.Path(os.environ.get("DATA_DIR", "/var/lib/horizon-mod-build"))
HOST = os.environ.get("HOST", "0.0.0.0")
PORT = int(os.environ.get("PORT", "8080"))
MAX_PENDING = int(os.environ.get("MAX_PENDING", "3"))
MAX_PER_IP_HOUR = int(os.environ.get("MAX_PER_IP_HOUR", "3"))
REQUEST_TTL = 60 * 60
ID_RE = re.compile(r"^[A-Za-z0-9_-]{24,80}$")
LOCK = threading.RLock()
IP_REQUESTS: dict[str, list[float]] = {}


def json_bytes(value: object) -> bytes:
    return json.dumps(value, separators=(",", ":")).encode("utf-8")


def github(method: str, path: str, payload: dict | None = None):
    if not TOKEN:
        raise RuntimeError("GITHUB_TOKEN is not configured on the gateway")
    data = None if payload is None else json_bytes(payload)
    request = urllib.request.Request(
        "https://api.github.com" + path,
        data=data,
        method=method,
        headers={
            "Authorization": "Bearer " + TOKEN,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "Horizon-Native-Mod-Build-Gateway",
            "Content-Type": "application/json",
        },
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        raw = response.read(16 << 20)
        return response.status, raw


def load_state(ask: str) -> dict | None:
    path = DATA / "requests" / (ask + ".json")
    try:
        return json.loads(path.read_text())
    except Exception:
        return None


def save_state(state: dict) -> None:
    folder = DATA / "requests"
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / (state["id"] + ".json")
    temp = path.with_suffix(".tmp")
    temp.write_text(json.dumps(state, indent=2))
    temp.replace(path)


def clean_old() -> None:
    cutoff = time.time() - REQUEST_TTL
    for path in (DATA / "requests").glob("*.json"):
        try:
            if json.loads(path.read_text()).get("created", 0) < cutoff:
                ask = path.stem
                path.unlink(missing_ok=True)
                (DATA / "requests" / (ask + ".bin")).unlink(missing_ok=True)
                shutil.rmtree(DATA / "builds" / ask, ignore_errors=True)
        except Exception:
            continue


def current_pending() -> int:
    count = 0
    for path in (DATA / "requests").glob("*.json"):
        try:
            state = json.loads(path.read_text())
            if state.get("state") in ("starting", "queued", "building"):
                count += 1
        except Exception:
            pass
    return count


def find_run(ask: str) -> dict | None:
    query = urllib.parse.urlencode({"event": "workflow_dispatch", "branch": REF, "per_page": 100})
    _, raw = github("GET", f"/repos/{REPOSITORY}/actions/workflows/{WORKFLOW}/runs?{query}")
    runs = json.loads(raw).get("workflow_runs", [])
    title = "Horizon native mod build " + ask
    return next((run for run in runs if run.get("display_title") == title), None)


def failed_step(run_id: int) -> str:
    try:
        _, raw = github("GET", f"/repos/{REPOSITORY}/actions/runs/{run_id}/jobs?per_page=100")
        jobs = json.loads(raw).get("jobs", [])
        for job in jobs:
            for step in job.get("steps", []):
                if step.get("conclusion") == "failure":
                    return f"Build failed at: {step.get('name', 'unknown step')}"
    except Exception:
        pass
    return "The GitHub build failed. Check its Actions log."


def artifact_bytes(run_id: int, name: str) -> bytes:
    _, raw = github("GET", f"/repos/{REPOSITORY}/actions/runs/{run_id}/artifacts?per_page=100")
    artifacts = json.loads(raw).get("artifacts", [])
    artifact = next((a for a in artifacts if a.get("name") == name and not a.get("expired")), None)
    if artifact is None:
        raise FileNotFoundError("The completed build artifact is not available yet")
    url = artifact.get("archive_download_url")
    if not url:
        raise RuntimeError("GitHub did not provide an artifact download URL")
    request = urllib.request.Request(url, headers={
        "Authorization": "Bearer " + TOKEN,
        "Accept": "application/vnd.github+json",
        "User-Agent": "Horizon-Native-Mod-Build-Gateway",
    })
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            return None
    opener = urllib.request.build_opener(NoRedirect)
    try:
        response = opener.open(request, timeout=60)
        data = response.read(512 << 20)
    except urllib.error.HTTPError as error:
        if error.code not in (301, 302, 303, 307, 308):
            raise
        location = error.headers.get("Location")
        if not location:
            raise RuntimeError("GitHub artifact redirect has no Location")
        with urllib.request.urlopen(location, timeout=120) as response:
            data = response.read(512 << 20)
    if not data:
        raise RuntimeError("GitHub returned an empty build artifact")
    return data


def finish_artifact(ask: str, run: dict, state: dict) -> dict:
    data = artifact_bytes(run["id"], "horizon-native-build-" + ask)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = set(archive.namelist())
        if "libhorizon_android.so" not in names or "build.json" not in names:
            raise RuntimeError("Build artifact is missing its library or metadata")
        library = archive.read("libhorizon_android.so")
        metadata = json.loads(archive.read("build.json"))
    digest = hashlib.sha256(library).hexdigest()
    version_info = json.loads((ROOT / "versions.json").read_text())[state["version"]]
    expected_mods = sorted(
        {"simple-voice-chat", "horizon-native-builder", *state.get("mod_ids", [])}
    )
    actual_mods = [
        item.get("id") for item in metadata.get("mods", [])
        if isinstance(item, dict)
    ]
    if (
        metadata.get("sha256") != digest
        or metadata.get("size") != len(library)
        or metadata.get("version") != state["version"]
        or metadata.get("game") != version_info["game"]
        or metadata.get("hooks") != version_info["hooks"]
        or metadata.get("commit") != state["commit"]
        or metadata.get("horizon_mixins") != version_info["mixins"]
        or metadata.get("abi") != "arm64-v8a"
        or metadata.get("profile") != "fast"
        or actual_mods != expected_mods
    ):
        raise RuntimeError("Build artifact failed SHA-256, compatibility or mod-set verification")
    folder = DATA / "builds" / ask
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "libhorizon_android.so").write_bytes(library)
    (folder / "build.json").write_text(json.dumps(metadata, indent=2))
    state.update({
        "state": "done",
        "sha256": digest,
        "size": len(library),
        "build": metadata,
        "run_id": run["id"],
        "updated": time.time(),
    })
    save_state(state)
    (DATA / "requests" / (ask + ".bin")).unlink(missing_ok=True)
    return state


def status(ask: str) -> dict:
    state = load_state(ask)
    if state is None:
        raise KeyError("Unknown or expired build request")
    if state.get("state") in ("done", "failed"):
        return state
    if time.time() - state.get("last_poll", 0) < 2:
        return state
    state["last_poll"] = time.time()
    try:
        run = find_run(ask)
        if run is None:
            if time.time() - state["created"] > 300:
                state.update({"state": "failed", "error": "GitHub did not start the requested build"})
            else:
                state["state"] = "starting"
        elif run.get("status") != "completed":
            state.update({"state": "queued" if run.get("status") == "queued" else "building", "run_id": run["id"]})
        elif run.get("conclusion") != "success":
            state.update({"state": "failed", "error": failed_step(run["id"]), "run_id": run["id"]})
        else:
            try:
                return finish_artifact(ask, run, state)
            except FileNotFoundError:
                state.update({"state": "building", "run_id": run["id"]})
            except Exception as error:
                state.update({"state": "failed", "error": str(error), "run_id": run["id"]})
    except Exception as error:
        state["gateway_error"] = str(error)
    save_state(state)
    return state


class Handler(http.server.BaseHTTPRequestHandler):
    server_version = "HorizonBuildGateway/1"

    def log_message(self, format, *args):
        print("%s - %s" % (self.address_string(), format % args), flush=True)

    def reply(self, code: int, value: object) -> None:
        data = json_bytes(value)
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path.strip("/")
        if path == "health":
            return self.reply(200, {"ok": True})
        parts = path.split("/")
        if len(parts) == 3 and parts[:2] == ["v1", "request"]:
            ask = parts[2]
            if not ID_RE.fullmatch(ask):
                return self.reply(404, {"error": "unknown request"})
            file = DATA / "requests" / (ask + ".bin")
            if not file.is_file():
                return self.reply(404, {"error": "request expired"})
            data = file.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(data)
            return
        if len(parts) in (3, 4) and parts[:2] == ["v1", "job"]:
            ask = parts[2]
            if not ID_RE.fullmatch(ask):
                return self.reply(404, {"error": "unknown request"})
            try:
                state = status(ask)
            except KeyError as error:
                return self.reply(404, {"error": str(error)})
            if len(parts) == 3:
                return self.reply(200, {k: v for k, v in state.items() if k not in ("last_poll", "created")})
            if parts[3] != "lib" or state.get("state") != "done":
                return self.reply(409, {"error": "build is not ready"})
            library = DATA / "builds" / ask / "libhorizon_android.so"
            if not library.is_file():
                return self.reply(404, {"error": "build library expired"})
            data = library.read_bytes()
            start, end, code = 0, len(data) - 1, 200
            header = self.headers.get("Range")
            if header and header.startswith("bytes="):
                try:
                    first, last = header[6:].split("-", 1)
                    start = int(first or "0")
                    end = min(int(last) if last else len(data) - 1, len(data) - 1)
                    if start < 0 or end < start or start >= len(data):
                        raise ValueError()
                    code = 206
                except ValueError:
                    return self.reply(416, {"error": "invalid byte range"})
            body = data[start:end + 1]
            self.send_response(code)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Accept-Ranges", "bytes")
            if code == 206:
                self.send_header("Content-Range", f"bytes {start}-{end}/{len(data)}")
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)
            return
        self.reply(404, {"error": "not found"})

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path != "/v1/build":
            return self.reply(404, {"error": "not found"})
        query = urllib.parse.parse_qs(parsed.query)
        version = query.get("version", [""])[0]
        if version != "0.7.8":
            return self.reply(400, {"error": "unsupported Horizon version"})
        if not PUBLIC_URL.startswith("https://"):
            return self.reply(503, {"error": "gateway HTTPS public URL is not configured"})
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            return self.reply(400, {"error": "invalid Content-Length"})
        if length <= 0 or length > 32 << 20:
            return self.reply(413, {"error": "request must be between 1 byte and 32 MiB"})
        ip = self.client_address[0]
        now = time.time()
        with LOCK:
            clean_old()
            history = [t for t in IP_REQUESTS.get(ip, []) if now - t < 3600]
            if len(history) >= MAX_PER_IP_HOUR:
                return self.reply(429, {"error": "build rate limit reached"})
            if current_pending() >= MAX_PENDING:
                return self.reply(429, {"error": "build queue is full; try again later"})
            IP_REQUESTS[ip] = history + [now]
        body = self.rfile.read(length)
        if len(body) != length:
            return self.reply(400, {"error": "incomplete request body"})
        ask = secrets.token_urlsafe(24).replace("-", "_")
        version_file = ROOT / "versions.json"
        version_info = json.loads(version_file.read_text()).get(version)
        if not version_info:
            return self.reply(400, {"error": "unsupported Horizon version"})
        commit = version_info["horizon"]
        with tempfile.TemporaryDirectory(prefix="horizon-validate-") as temp:
            try:
                manifests = unpack_request(body, pathlib.Path(temp) / "mods")
                check_compatibility(manifests, version_info)
            except Exception as error:
                return self.reply(400, {"error": str(error)})
        folder = DATA / "requests"
        folder.mkdir(parents=True, exist_ok=True)
        (folder / (ask + ".bin")).write_bytes(body)
        state = {
            "id": ask, "version": version, "commit": commit,
            "mod_ids": sorted(m["id"] for m in manifests),
            "state": "starting", "created": now, "updated": now,
        }
        save_state(state)
        inputs = {
            "version": version,
            "request_id": ask,
            "request_url": PUBLIC_URL + "/v1/request/" + ask,
        }
        try:
            status_code, _ = github(
                "POST",
                f"/repos/{REPOSITORY}/actions/workflows/{WORKFLOW}/dispatches",
                {"ref": REF, "inputs": inputs},
            )
            if status_code not in (200, 201, 204):
                raise RuntimeError(f"GitHub dispatch returned {status_code}")
        except Exception as error:
            (folder / (ask + ".bin")).unlink(missing_ok=True)
            state.update({"state": "failed", "error": "could not start GitHub build: " + str(error)})
            save_state(state)
            return self.reply(502, {"error": state["error"]})
        self.reply(202, {"job": ask, "state": "starting"})

    def do_HEAD(self):
        return self.reply(405, {"error": "method not allowed"})


if __name__ == "__main__":
    if not TOKEN or not PUBLIC_URL.startswith("https://"):
        raise SystemExit("Set GITHUB_TOKEN and PUBLIC_BASE_URL=https://your-host before starting.")
    (DATA / "requests").mkdir(parents=True, exist_ok=True)
    (DATA / "builds").mkdir(parents=True, exist_ok=True)
    print(f"Horizon build gateway listening on {HOST}:{PORT}", flush=True)
    http.server.ThreadingHTTPServer((HOST, PORT), Handler).serve_forever()
