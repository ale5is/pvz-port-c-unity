use super::{native_hooks::NativeMod, Game};
use sha2::{Digest, Sha256};
use std::{
    collections::HashSet,
    fs::{self, File},
    io::{Read, Write},
    net::IpAddr,
    sync::{Mutex, OnceLock, PoisonError},
    thread,
    time::{Duration, Instant},
};

const BUILDER_ID: &str = "horizon-native-builder";
const GAME_VERSION: &str = "0.7.8";
const NATIVE_MODS_DIR: &str = "horizon-native-mods";
const REQUEST_LIMIT: usize = 32 << 20;
const MAX_MODS: usize = 16;
const POLL: Duration = Duration::from_secs(2);
const BUILD_LIMIT: Duration = Duration::from_secs(40 * 60);

#[derive(Default)]
struct BuildState {
    running: bool,
    status: String,
    error: Option<String>,
}

fn shared() -> &'static Mutex<BuildState> {
    static STATE: OnceLock<Mutex<BuildState>> = OnceLock::new();
    STATE.get_or_init(|| Mutex::new(BuildState::default()))
}

fn state_lock() -> std::sync::MutexGuard<'static, BuildState> {
    shared().lock().unwrap_or_else(PoisonError::into_inner)
}

fn set_status(status: impl Into<String>) {
    state_lock().status = status.into();
}

fn short(text: &str) -> String {
    let value: String = text.chars().take(72).collect();
    if value.chars().count() < text.chars().count() {
        format!("{value}…")
    } else {
        value
    }
}

pub(super) fn add_mod(
    _game: &mut Game<'_>,
    mods: &mut Vec<NativeMod>,
) {
    if mods.iter().any(|m| m.id == BUILDER_ID) {
        return;
    }
    mods.push(NativeMod {
        id: BUILDER_ID.into(),
        title: "Native Mod Builder".into(),
        description: "Compile installed .hnmod packages for Horizon 0.7.8 on a separate build server. Configure horizon-native-mods/build-server.txt first.".into(),
        keys: Vec::new(),
    });
}

pub(super) fn settings(
    _game: &mut Game<'_>,
    mod_id: &str,
    buttons: &mut Vec<String>,
) {
    if mod_id != BUILDER_ID {
        return;
    }
    let state = state_lock();
    let action = if state.running {
        "Build running"
    } else if state.error.is_some() {
        "Retry compilation"
    } else {
        "Compile installed native mods"
    };
    let status = if state.status.is_empty() {
        "Idle".to_owned()
    } else {
        short(&state.status)
    };
    buttons.push(format!("{action} — {status}"));
}

pub(super) fn settings_click(
    _game: &mut Game<'_>,
    mod_id: &str,
    button: usize,
) {
    if mod_id == BUILDER_ID && button == 0 {
        start_compile();
    }
}

fn start_compile() {
    {
        let mut state = state_lock();
        if state.running {
            return;
        }
        state.running = true;
        state.status = "Preparing native mod packages…".into();
        state.error = None;
    }
    let spawned = thread::Builder::new()
        .name("horizon-native-mod-build".into())
        .spawn(|| {
            let result = compile_installed();
            let mut state = state_lock();
            state.running = false;
            match result {
                Ok(name) => {
                    state.status = format!("Ready: {name}. Restart Horizon to load this build.");
                    state.error = None;
                }
                Err(error) => {
                    state.status = format!("Build failed: {error}");
                    state.error = Some(error);
                }
            }
        });
    if let Err(error) = spawned {
        let mut state = state_lock();
        state.running = false;
        state.error = Some(error.to_string());
        state.status = format!("Could not start build worker: {error}");
    }
}

fn server_url(home: &std::path::Path) -> Result<String, String> {
    let path = home.join(NATIVE_MODS_DIR).join("build-server.txt");
    let text = fs::read_to_string(&path)
        .map_err(|_| format!("Create {} with your build server URL.", path.display()))?;
    let text = text.trim();
    let parsed = url::Url::parse(text).map_err(|e| format!("Invalid build server URL: {e}"))?;
    if !parsed.username().is_empty()
        || parsed.password().is_some()
        || parsed.path() != "/"
        || parsed.query().is_some()
        || parsed.fragment().is_some()
    {
        return Err("Build server URL must contain only scheme, host and optional port.".into());
    }
    match parsed.scheme() {
        "https" => {}
        "http" => {
            let host = parsed.host_str().ok_or("Build server has no host")?;
            let local = host == "localhost"
                || host.parse::<IpAddr>().is_ok_and(|ip| match ip {
                    IpAddr::V4(v4) => v4.is_private() || v4.is_loopback(),
                    IpAddr::V6(v6) => v6.is_loopback() || (v6.segments()[0] & 0xfe00) == 0xfc00,
                });
            if !local {
                return Err("HTTP is allowed only for localhost or a private LAN IP; use HTTPS elsewhere.".into());
            }
        }
        _ => return Err("Build server must use HTTPS (or HTTP on a private LAN).".into()),
    }
    Ok(text.trim_end_matches('/').to_owned())
}

fn service_error(error: ureq::Error) -> String {
    match error {
        ureq::Error::Status(code, response) => response
            .into_string()
            .ok()
            .and_then(|text| serde_json::from_str::<serde_json::Value>(&text).ok())
            .and_then(|value| value["error"].as_str().map(str::to_owned))
            .unwrap_or_else(|| format!("Build server returned HTTP {code}.")),
        ureq::Error::Transport(error) => format!("Build server connection failed: {error}"),
    }
}

fn bundles_body(home: &std::path::Path) -> Result<Vec<u8>, String> {
    let folder = home.join(NATIVE_MODS_DIR);
    fs::create_dir_all(&folder).map_err(|e| e.to_string())?;
    let entries = fs::read_dir(&folder)
        .map_err(|e| format!("Cannot read {}: {e}", folder.display()))?;
    let mut mods = Vec::new();
    for entry in entries {
        let entry = entry.map_err(|e| e.to_string())?;
        let kind = entry.file_type().map_err(|e| e.to_string())?;
        if !kind.is_file() || kind.is_symlink() {
            continue;
        }
        let path = entry.path();
        if !path.extension().is_some_and(|ext| ext == "hnmod") {
            continue;
        }
        let bundle = horizon_mods::index::native::Bundle::read(&path)
            .map_err(|e| format!("{}: {e}", path.display()))?;
        let bytes = fs::read(&path).map_err(|e| format!("{}: {e}", path.display()))?;
        mods.push((bundle.manifest.id, bytes));
    }
    mods.sort_by(|a, b| a.0.cmp(&b.0));
    if mods.is_empty() {
        return Err("No .hnmod packages found in horizon-native-mods.".into());
    }
    if mods.len() > MAX_MODS {
        return Err(format!("At most {MAX_MODS} native mods can be compiled together."));
    }
    let mut body = Vec::new();
    let mut seen = HashSet::new();
    for (id, bytes) in mods {
        if !seen.insert(id.clone()) {
            return Err(format!("Duplicate native mod ID: {id}"));
        }
        if bytes.is_empty() || bytes.len() > u32::MAX as usize {
            return Err(format!("Invalid package size for {id}."));
        }
        body.extend_from_slice(&(bytes.len() as u32).to_be_bytes());
        body.extend_from_slice(&bytes);
        if body.len() > REQUEST_LIMIT {
            return Err("Combined native mods exceed the 32 MiB build request limit.".into());
        }
    }
    Ok(body)
}

fn compile_installed() -> Result<String, String> {
    let config = Game::native_config_dir()?;
    let home = config
        .parent()
        .ok_or("Horizon config directory has no application-data parent.")?
        .to_path_buf();
    let server = server_url(&home)?;
    let stamp = std::env::var("HORIZON_APK")
        .ok()
        .filter(|s| !s.is_empty())
        .ok_or("Android did not provide the APK install stamp; cannot safely activate a build.")?;
    let body = bundles_body(&home)?;
    let agent = ureq::AgentBuilder::new()
        .timeout_connect(Duration::from_secs(20))
        .timeout_read(Duration::from_secs(45))
        .build();

    set_status("Uploading .hnmod packages to the build server…");
    let response = agent
        .post(&format!("{server}/v1/build?version={GAME_VERSION}"))
        .set("Content-Type", "application/octet-stream")
        .send_bytes(&body)
        .map_err(service_error)?;
    let answer = response.into_string().map_err(|e| e.to_string())?;
    let answer: serde_json::Value =
        serde_json::from_str(&answer).map_err(|e| format!("Invalid build response: {e}"))?;
    let job = answer["job"]
        .as_str()
        .filter(|id| id.len() == 24 && id.bytes().all(|b| b.is_ascii_hexdigit()))
        .ok_or("Build server did not return a valid job ID.")?
        .to_owned();

    let started = Instant::now();
    let finished = loop {
        if started.elapsed() > BUILD_LIMIT {
            return Err("Build timed out after 40 minutes.".into());
        }
        thread::sleep(POLL);
        let response = agent
            .get(&format!("{server}/v1/job/{job}"))
            .call()
            .map_err(service_error)?;
        let text = response.into_string().map_err(|e| e.to_string())?;
        let status: serde_json::Value =
            serde_json::from_str(&text).map_err(|e| format!("Invalid job status: {e}"))?;
        match status["state"].as_str() {
            Some("queued") => {
                let ahead = status["ahead"].as_u64().unwrap_or(0);
                set_status(format!("Queued: {ahead} build(s) ahead…"));
            }
            Some("building") => {
                let seconds = status["seconds"].as_u64().unwrap_or(0);
                set_status(format!("Compiling native mods… {seconds}s"));
            }
            Some("done") => break status,
            Some("failed") => {
                return Err(status["error"]
                    .as_str()
                    .unwrap_or("The build server reported a failure.")
                    .to_owned());
            }
            _ => return Err("Build server returned an unknown job state.".into()),
        }
    };

    let sha = finished["sha256"]
        .as_str()
        .filter(|s| s.len() == 64 && s.bytes().all(|b| b.is_ascii_hexdigit()))
        .ok_or("Build server did not return a valid SHA-256.")?
        .to_ascii_lowercase();
    let size = finished["size"]
        .as_u64()
        .filter(|size| *size > 0 && *size <= 256 << 20)
        .ok_or("Build server returned an invalid library size.")?;
    let build_info = finished["build"].clone();
    let native = home.join("native");
    let name = format!("compiled-{job}");
    let folder = native.join(&name);
    fs::create_dir_all(&folder).map_err(|e| e.to_string())?;
    let part = folder.join("libhorizon_android.so.part");
    let library = folder.join("libhorizon_android.so");

    set_status("Downloading and verifying the compiled ARM64 library…");
    let result = (|| -> Result<(), String> {
        let response = agent
            .get(&format!("{server}/v1/job/{job}/lib"))
            .call()
            .map_err(service_error)?;
        let mut reader = response.into_reader();
        let mut file = File::create(&part).map_err(|e| e.to_string())?;
        let mut hash = Sha256::new();
        let mut total = 0u64;
        let mut buffer = vec![0u8; 256 * 1024];
        loop {
            let count = reader.read(&mut buffer).map_err(|e| e.to_string())?;
            if count == 0 {
                break;
            }
            total = total.saturating_add(count as u64);
            if total > size {
                return Err("Build server sent more bytes than declared.".into());
            }
            file.write_all(&buffer[..count]).map_err(|e| e.to_string())?;
            hash.update(&buffer[..count]);
        }
        file.sync_all().map_err(|e| e.to_string())?;
        if total != size {
            return Err(format!("Incomplete library download: {total} of {size} bytes."));
        }
        let actual = hash.finalize().iter().map(|b| format!("{b:02x}")).collect::<String>();
        if actual != sha {
            return Err("Compiled library SHA-256 does not match the build server.".into());
        }
        fs::rename(&part, &library).map_err(|e| e.to_string())?;
        fs::write(folder.join("for"), GAME_VERSION).map_err(|e| e.to_string())?;
        fs::write(folder.join("apk"), &stamp).map_err(|e| e.to_string())?;
        fs::write(
            folder.join("build.json"),
            serde_json::to_vec_pretty(&build_info).map_err(|e| e.to_string())?,
        )
        .map_err(|e| e.to_string())?;
        let current_part = native.join("current.part");
        fs::write(&current_part, &name).map_err(|e| e.to_string())?;
        fs::rename(current_part, native.join("current")).map_err(|e| e.to_string())?;
        Ok(())
    })();
    if result.is_err() {
        let _ = fs::remove_file(&part);
    }
    result?;
    Ok(name)
}
