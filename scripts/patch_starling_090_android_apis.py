#!/usr/bin/env python3
"""Backport Starling 0.9 Android frame hints and its frame-rate hook to Horizon 0.7.8."""
from pathlib import Path
import json
import sys


def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one exact match, found {count}")
    return source.replace(old, new, 1)


DEVICE_RUNTIME = r'''//! Optional native frame-rate requests, gyro, ADPF and thermal headroom.
#[path = "device_gyro.rs"]
mod gyro_sensor;
#[path = "device_math.rs"]
mod math;
use std::{
    cell::RefCell,
    ffi::{c_char, c_int, c_void, CStr},
    sync::OnceLock,
    time::Duration,
};
#[link(name = "dl")]
unsafe extern "C" {
    fn dlopen(n: *const c_char, f: c_int) -> *mut c_void;
    fn dlsym(h: *mut c_void, n: *const c_char) -> *mut c_void;
    fn gettid() -> c_int;
}

struct Runtime {
    window: usize,
    hz: u16,
    voted_rate: f32,
    rate_status: i32,
    gyro: Option<gyro_sensor::Gyroscope>,
    session: usize,
    hint_failed: bool,
    target: i64,
    adpf_reports: u64,
    adpf_status: i32,
}

impl Default for Runtime {
    fn default() -> Self {
        Self {
            window: 0,
            hz: u16::MAX,
            voted_rate: -1.0,
            rate_status: -1,
            gyro: None,
            session: 0,
            hint_failed: false,
            target: 0,
            adpf_reports: 0,
            adpf_status: 0,
        }
    }
}

impl Drop for Runtime {
    fn drop(&mut self) {
        self.close_hint(frame_hints());
    }
}

thread_local! {
    static STATE: RefCell<Runtime> = RefCell::new(Runtime::default());
}

struct FrameHints {
    _lib: usize,
    manager: usize,
    create: Option<unsafe extern "C" fn(usize, *const i32, usize, i64) -> usize>,
    update: Option<unsafe extern "C" fn(usize, i64) -> c_int>,
    report: Option<unsafe extern "C" fn(usize, i64) -> c_int>,
    close: Option<unsafe extern "C" fn(usize)>,
    set_frame_rate: Option<unsafe extern "C" fn(*mut c_void, f32, i8) -> c_int>,
}

fn frame_hints() -> &'static FrameHints {
    static HINTS: OnceLock<FrameHints> = OnceLock::new();
    HINTS.get_or_init(|| unsafe {
        let lib = dlopen(c"libandroid.so".as_ptr(), 2);
        let symbol = |name: &CStr| {
            let ptr = if lib.is_null() {
                std::ptr::null_mut()
            } else {
                dlsym(lib, name.as_ptr())
            };
            (!ptr.is_null()).then_some(ptr)
        };
        let manager = symbol(c"APerformanceHint_getManager")
            .map(|ptr| {
                let get: unsafe extern "C" fn() -> usize = std::mem::transmute(ptr);
                get()
            })
            .unwrap_or(0);
        FrameHints {
            _lib: lib as usize,
            manager,
            create: symbol(c"APerformanceHint_createSession").map(|ptr| {
                std::mem::transmute::<
                    *mut c_void,
                    unsafe extern "C" fn(usize, *const i32, usize, i64) -> usize,
                >(ptr)
            }),
            update: symbol(c"APerformanceHint_updateTargetWorkDuration").map(|ptr| {
                std::mem::transmute::<*mut c_void, unsafe extern "C" fn(usize, i64) -> c_int>(
                    ptr,
                )
            }),
            report: symbol(c"APerformanceHint_reportActualWorkDuration").map(|ptr| {
                std::mem::transmute::<*mut c_void, unsafe extern "C" fn(usize, i64) -> c_int>(
                    ptr,
                )
            }),
            close: symbol(c"APerformanceHint_closeSession").map(|ptr| {
                std::mem::transmute::<*mut c_void, unsafe extern "C" fn(usize)>(ptr)
            }),
            set_frame_rate: symbol(c"ANativeWindow_setFrameRate").map(|ptr| {
                std::mem::transmute::<
                    *mut c_void,
                    unsafe extern "C" fn(*mut c_void, f32, i8) -> c_int,
                >(ptr)
            }),
        }
    })
}

struct Thermal {
    _lib: usize,
    manager: usize,
    status: Option<unsafe extern "C" fn(usize) -> c_int>,
    headroom: Option<unsafe extern "C" fn(usize, c_int) -> f32>,
}

fn thermal() -> Option<&'static Thermal> {
    static THERMAL: OnceLock<Option<Thermal>> = OnceLock::new();
    THERMAL
        .get_or_init(|| unsafe {
            let lib = dlopen(c"libandroid.so".as_ptr(), 2);
            if lib.is_null() {
                return None;
            }
            let acquire = dlsym(lib, c"AThermal_acquireManager".as_ptr());
            if acquire.is_null() {
                return None;
            }
            let acquire: unsafe extern "C" fn() -> usize = std::mem::transmute(acquire);
            let manager = acquire();
            if manager == 0 {
                return None;
            }
            let symbol = |name: &CStr| {
                let ptr = dlsym(lib, name.as_ptr());
                (!ptr.is_null()).then_some(ptr)
            };
            Some(Thermal {
                _lib: lib as usize,
                manager,
                status: symbol(c"AThermal_getCurrentThermalStatus").map(|ptr| {
                    std::mem::transmute::<*mut c_void, unsafe extern "C" fn(usize) -> c_int>(
                        ptr,
                    )
                }),
                headroom: symbol(c"AThermal_getThermalHeadroom").map(|ptr| {
                    std::mem::transmute::<
                        *mut c_void,
                        unsafe extern "C" fn(usize, c_int) -> f32,
                    >(ptr)
                }),
            })
        })
        .as_ref()
}

fn thermal_status() -> Option<i32> {
    let thermal = thermal()?;
    thermal.status.map(|status| unsafe { status(thermal.manager) })
}

fn thermal_headroom() -> Option<f32> {
    let thermal = thermal()?;
    thermal
        .headroom
        .map(|headroom| unsafe { headroom(thermal.manager, 10) })
        .filter(|value| value.is_finite())
}

fn max_refresh_rate() -> f32 {
    static RATE: OnceLock<f32> = OnceLock::new();
    *RATE.get_or_init(|| {
        crate::platform::with_context(|env, activity| {
            let manager = env
                .call_method(
                    activity,
                    "getWindowManager",
                    "()Landroid/view/WindowManager;",
                    &[],
                )?
                .l()?;
            let display = env
                .call_method(
                    &manager,
                    "getDefaultDisplay",
                    "()Landroid/view/Display;",
                    &[],
                )?
                .l()?;
            let sdk = env
                .get_static_field("android/os/Build$VERSION", "SDK_INT", "I")?
                .i()?;
            let current = env.call_method(&display, "getRefreshRate", "()F", &[])?.f()?;
            let current = if current.is_finite() && current > 0.0 {
                current
            } else {
                0.0
            };
            if sdk < 23 {
                return Ok(current);
            }
            let modes = env
                .call_method(
                    &display,
                    "getSupportedModes",
                    "()[Landroid/view/Display$Mode;",
                    &[],
                )?
                .l()?;
            let modes = jni::objects::JObjectArray::from(modes);
            let count = env.get_array_length(&modes)?;
            let mut maximum = current;
            for index in 0..count {
                let mode = env.get_object_array_element(&modes, index)?;
                let rate = env.call_method(&mode, "getRefreshRate", "()F", &[])?.f()?;
                if rate.is_finite() {
                    maximum = maximum.max(rate);
                }
            }
            Ok(maximum)
        })
        .unwrap_or(0.0)
    })
}

impl Runtime {
    fn close_hint(&mut self, hints: &FrameHints) {
        if self.session != 0 {
            if let Some(close) = hints.close {
                unsafe { close(self.session) };
            }
            self.session = 0;
        }
    }
}

/// Report completed CPU frame work to Android's scheduler and vote for the display rate.
/// Dynamic symbols are resolved once; the frame path does not allocate or perform JNI calls.
const REQUEST_NATIVE_DISPLAY_RATE: bool = false;

pub fn frame_hint(cpu: Duration, fps_limit: i32, active: bool) {
    let settings = preferences();
    let hints = frame_hints();
    let cap = (fps_limit < 260).then(|| fps_limit.clamp(10, 240));
    let target_ns = 1_000_000_000i64 / i64::from(cap.unwrap_or(120));
    let requested_rate = if !active {
        0.0
    } else if settings.display_hz != 0 {
        f32::from(settings.display_hz)
    } else if let Some(rate) = cap {
        rate as f32
    } else if hints.set_frame_rate.is_some() {
        max_refresh_rate()
    } else {
        0.0
    };

    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        if !active || !settings.performance_hints {
            state.close_hint(hints);
            state.hint_failed = false;
        } else {
            if state.session == 0 && !state.hint_failed {
                if hints.manager != 0 {
                    if let Some(create) = hints.create {
                        let tid = unsafe { gettid() };
                        state.session = unsafe { create(hints.manager, &tid, 1, target_ns) };
                    }
                }
                state.hint_failed = state.session == 0;
                state.target = target_ns;
            }
            if state.session != 0 {
                unsafe {
                    if state.target != target_ns {
                        if let Some(update) = hints.update {
                            update(state.session, target_ns);
                        }
                        state.target = target_ns;
                    }
                    if let Some(report) = hints.report {
                        state.adpf_status = report(
                            state.session,
                            (cpu.as_nanos().min(i64::MAX as u128) as i64).max(1),
                        );
                        state.adpf_reports += 1;
                    }
                }
            }
        }

        if REQUEST_NATIVE_DISPLAY_RATE {
            if let Some(set_rate) = hints.set_frame_rate {
                crate::APP.with(|app| {
                    if let Some(window) = app.borrow().as_ref().and_then(|app| app.native_window()) {
                        let pointer = window.ptr().as_ptr() as usize;
                        if state.window != pointer || state.voted_rate != requested_rate {
                            state.rate_status =
                                unsafe { set_rate(pointer as *mut c_void, requested_rate, 0) };
                            state.window = pointer;
                            state.voted_rate = requested_rate;
                            state.hz = requested_rate.round().clamp(0.0, u16::MAX as f32) as u16;
                        }
                    }
                });
            }
        }
    });
}

/// Gyro uses Android device axes, remapped by current display rotation.
pub fn gyro(enabled: bool, sensitivity: f32, rotation: i32) -> [f32; 2] {
    STATE.with(|slot| {
        let mut state = slot.borrow_mut();
        if enabled && state.gyro.is_none() {
            state.gyro = gyro_sensor::Gyroscope::spawn();
        }
        state
            .gyro
            .as_mut()
            .map_or([0.; 2], |gyro| gyro.read(enabled, sensitivity, rotation))
    })
}

pub fn diagnostics() -> serde_json::Value {
    STATE.with(|slot| {
        let state = slot.borrow();
        serde_json::json!({
            "display_hz_requested": state.hz,
            "display_request_result": state.rate_status,
            "adpf_session_active": state.session != 0,
            "adpf_unavailable": state.hint_failed,
            "gyro_available": state.gyro.as_ref().is_some_and(|gyro| gyro.available()),
            "gyro_active": state.gyro.as_ref().is_some_and(|gyro| gyro.active()),
            "gyro_samples": state.gyro.as_ref().map_or(0, |gyro| gyro.samples),
            "adpf_report_count": state.adpf_reports,
            "adpf_report_status": state.adpf_status,
            "thermal_status": thermal_status(),
            "thermal_headroom_10s": thermal_headroom(),
        })
    })
}

/// Release sensor delivery and hint sessions immediately when the window loses focus.
pub fn suspend() {
    let _ = gyro(false, 1.0, 0);
    frame_hint(Duration::ZERO, 0, false);
}

// One atomic snapshot avoids mixing frequency and flags when the client changes
// its preferences. Touch profiles never own these system settings.
static PREFERENCES: std::sync::atomic::AtomicU32 = std::sync::atomic::AtomicU32::new(0);

pub fn configure(mut settings: crate::device_preferences::Settings) {
    settings.sanitize();
    let bits = u32::from(settings.display_hz)
        | (u32::from(settings.performance_hints) << 16)
        | (u32::from(settings.gpu_profile) << 17);
    PREFERENCES.store(bits, std::sync::atomic::Ordering::Relaxed);
}

pub fn preferences() -> crate::device_preferences::Settings {
    let bits = PREFERENCES.load(std::sync::atomic::Ordering::Relaxed);
    crate::device_preferences::Settings {
        display_hz: bits as u16,
        performance_hints: bits & (1 << 16) != 0,
        gpu_profile: bits & (1 << 17) != 0,
    }
}
'''


def patch_runtime(root: Path) -> None:
    path = root / "crates/horizon-client/src/android/device_runtime.rs"
    source = path.read_text(encoding="utf-8")
    if "pub fn frame_hint(" in source:
        raise SystemExit("Android frame hints are already present; refusing to apply twice.")
    if source.count("pub fn frame(enabled: bool, hz: u16, active: bool) -> Frame") != 1:
        raise SystemExit("Unexpected Horizon 0.7.8 device_runtime.rs; refusing to replace it.")
    if source.count("pub fn gyro(enabled: bool, sensitivity: f32, rotation: i32)") != 1:
        raise SystemExit("The pinned gyro API changed; refusing to replace device_runtime.rs.")
    path.write_text(DEVICE_RUNTIME, encoding="utf-8")

    game_path = root / "crates/horizon-client/src/game.rs"
    game = game_path.read_text(encoding="utf-8")
    game = replace_once(
        game,
        '''        #[cfg(target_os = "android")]
        let device_preferences = crate::device_runtime::preferences();
        #[cfg(target_os = "android")]
        let _native_frame = crate::device_runtime::frame(
            device_preferences.performance_hints,
            device_preferences.display_hz,
            self.focused,
        );
''',
        "",
        "Move Android frame hints from render entry to completed-frame accounting",
    )
    game = replace_once(
        game,
        '''                    #[cfg(target_os = "android")]
                    if let Some(sample) = performance_sample {
                        crate::performance::submit(sample, self.android_performance_context());
                    }
                    self.limit_frame();
''',
        '''                    let frame_limit = if self.options.frames.is_some()
                        || self.options.paced_by_split
                    {
                        rendered.then(|| self.framerate_limit())
                    } else {
                        Some(self.framerate_limit())
                    };
                    #[cfg(target_os = "android")]
                    if rendered {
                        if let Some(limit) = frame_limit {
                            let cpu = self.renderer.as_ref().map_or(Duration::ZERO, |renderer| {
                                now.elapsed().saturating_sub(renderer.last_wait)
                            });
                            crate::device_runtime::frame_hint(cpu, limit, self.focused);
                        }
                    }
                    #[cfg(target_os = "android")]
                    if let Some(sample) = performance_sample {
                        crate::performance::submit(sample, self.android_performance_context());
                    }
                    self.limit_frame(frame_limit);
''',
        "Report completed Android frame work before frame pacing",
    )
    game_path.write_text(game, encoding="utf-8")


def patch_framerate_hook(root: Path) -> None:
    hooks_path = root / "crates/horizon-client/src/game/native_hooks.rs"
    hooks = hooks_path.read_text(encoding="utf-8")
    hooks = replace_once(
        hooks,
        "//! API 80 adds the open screen hears of a mouse button going down or up before it acts",
        "//! API 81 adds the native frame-rate limit hook (native_framerate_limit): mods may lower\n//! the frame cap after the game's inactivity and Battery Mode limits.\n//!\n//! API 80 adds the open screen hears of a mouse button going down or up before it acts",
        "Document the additive native frame-rate hook API",
    )
    hooks = replace_once(
        hooks,
        "pub(crate) const NATIVE_HOOKS_API: u32 = 80;",
        "pub(crate) const NATIVE_HOOKS_API: u32 = 81;",
        "Additive native frame-rate hook API version",
    )
    hook = '''impl Game<'_> {
    /// The frame rate this window is about to be held to after inactivity and Battery Mode.
    /// Mods may lower the limit; values below one are ignored by the caller.
    #[inline(always)]
    pub(super) fn native_framerate_limit(
        &self,
        focused: bool,
        minimized: bool,
        in_world: bool,
        limit: &mut i32,
    ) {
        let _ = (focused, minimized, in_world, limit);
    }
}

'''
    if "native_framerate_limit(" in hooks:
        raise SystemExit("Native frame-rate hook already exists; refusing to duplicate it.")
    marker = "#[cfg(test)]"
    if hooks.count(marker) < 1:
        raise SystemExit("Could not find the native hook test boundary.")
    hooks = hooks.replace(marker, hook + marker, 1)
    hooks_path.write_text(hooks, encoding="utf-8")

    options_path = root / "crates/horizon-client/src/game/options.rs"
    options = options_path.read_text(encoding="utf-8")
    options = replace_once(
        options,
        '''        let limit = framerate_limit(self.settings.max_fps as i32, context);
        let limit = self
            .settings
            .horizon
            .battery_limit(limit, self.title.is_none(), self.focused);
        menu_frame_cap(limit, context.has_screen, cfg!(target_os = "android"))
''',
        '''        let limit = framerate_limit(self.settings.max_fps as i32, context);
        let limit = self
            .settings
            .horizon
            .battery_limit(limit, self.title.is_none(), self.focused);
        let limit = menu_frame_cap(limit, context.has_screen, cfg!(target_os = "android"));
        let mut mod_limit = limit;
        self.native_framerate_limit(
            context.focused,
            context.iconified,
            context.has_level,
            &mut mod_limit,
        );
        if mod_limit >= 1 && mod_limit < limit {
            mod_limit
        } else {
            limit
        }
''',
        "Apply the optional native frame-rate hook without allowing mods to raise the cap",
    )
    options = replace_once(
        options,
        '''    pub(super) fn limit_frame(&mut self) {
        if self.options.frames.is_some() || self.options.paced_by_split {
            return;
        }
        let limit = self.framerate_limit();
        match self.settings.horizon.pacing {
''',
        '''    pub(super) fn limit_frame(&mut self, limit: Option<i32>) {
        let Some(limit) = limit else {
            return;
        };
        if self.options.frames.is_some() || self.options.paced_by_split {
            return;
        }
        match self.settings.horizon.pacing {
''',
        "Reuse the already-computed frame limit for pacing",
    )
    options_path.write_text(options, encoding="utf-8")

    registry_path = root / "native-hooks.json"
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    hooks_list = registry.get("hooks")
    if not isinstance(hooks_list, list):
        raise SystemExit("Unexpected native-hooks.json schema.")
    if any(item.get("name") == "framerate_limit" for item in hooks_list):
        raise SystemExit("framerate_limit is already in native-hooks.json.")
    hooks_list.append({
        "name": "framerate_limit",
        "version": 1,
        "target": "horizon_client::game::native_hooks::Game<'_>::native_framerate_limit",
    })
    registry_path.write_text(json.dumps(registry, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: patch_starling_090_android_apis.py HORIZON_SOURCE")
    root = Path(sys.argv[1]).resolve()
    patch_runtime(root)
    patch_framerate_hook(root)
    print("Starling 0.9 Android APIs: cached ADPF/frame-rate/thermal lookups and API-81 frame-rate hook applied.")


if __name__ == "__main__":
    main()
