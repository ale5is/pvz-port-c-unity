#!/usr/bin/env python3
from pathlib import Path
import os
import shutil
import sys

root = Path(sys.argv[1]).resolve()

def patch_winit(root):
    cargo_home = Path(os.environ.get("CARGO_HOME", Path.home() / ".cargo"))
    sources = sorted((cargo_home / "registry" / "src").glob("*/winit-0.30.13"))
    if not sources:
        raise SystemExit("winit 0.30.13 source is missing; run cargo fetch before Android bootstrap")

    target = root / "target" / "pavohorizonnightly-winit"
    shutil.rmtree(target, ignore_errors=True)
    shutil.copytree(
        sources[0],
        target,
        ignore=shutil.ignore_patterns(".cargo-ok", ".cargo-checksum.json"),
    )

    def patch(path, old, new):
        file = target / path
        source = file.read_text()
        if new.strip() in source:
            return
        count = source.count(old)
        if count != 1:
            raise SystemExit(f"winit/{path}: expected one patch point, found {count}")
        file.write_text(source.replace(old, new))

    patch(
        "src/platform/android.rs",
        """/// Re-export of the `android_activity` API
""",
        """/// Horizon input hook: events the application consumes before winit translates them.
#[cfg(android_platform)]
pub type InputHook = fn(&activity::input::InputEvent<'_>) -> Option<activity::InputStatus>;

#[cfg(android_platform)]
pub fn set_input_hook(hook: InputHook) {
    crate::platform_impl::set_input_hook(hook);
}

/// Re-export of the `android_activity` API
""",
    )
    patch(
        "src/platform_impl/android/mod.rs",
        """static HAS_FOCUS: AtomicBool = AtomicBool::new(true);
""",
        """static HAS_FOCUS: AtomicBool = AtomicBool::new(true);
static INPUT_HOOK: std::sync::OnceLock<crate::platform::android::InputHook> =
    std::sync::OnceLock::new();

pub(crate) fn set_input_hook(hook: crate::platform::android::InputHook) {
    let _ = INPUT_HOOK.set(hook);
}
""",
    )
    patch(
        "src/platform_impl/android/mod.rs",
        """        F: FnMut(event::Event<T>, &RootAEL),
    {
        let mut input_status = InputStatus::Handled;
""",
        """        F: FnMut(event::Event<T>, &RootAEL),
    {
        if let Some(status) = INPUT_HOOK.get().and_then(|hook| hook(event)) {
            return status;
        }
        let mut input_status = InputStatus::Handled;
""",
    )


def replace_once(path, old, new):
    file = root / path
    source = file.read_text()
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}: {old!r}")
    file.write_text(source.replace(old, new))

replace_once(
    "Cargo.toml",
    '    "crates/horizon-client",\n',
    '    "crates/horizon-client",\n    "crates/horizon-android",\n',
)

replace_once(
    "crates/horizon-client/Cargo.toml",
    '[target.\'cfg(windows)\'.dependencies]\n',
    '[target.\'cfg(target_os = "android")\'.dependencies]\n'
    'winit = { version = "0.30.13", features = ["android-native-activity"] }\n\n'
    '[target.\'cfg(windows)\'.dependencies]\n',
)

replace_once(
    "Cargo.toml",
    'naga = { path = "third_party/naga" }\n',
    'naga = { path = "third_party/naga" }\n'
    'winit = { path = "target/pavohorizonnightly-winit" }\n',
)

replace_once(
    "crates/horizon-client/src/lib.rs",
    "extern crate self as horizon_client;\n",
    "extern crate self as horizon_client;\n"
    '#[cfg(target_os = "android")]\n'
    "pub mod android;\n",
)

replace_once(
    "crates/horizon-client-render/src/android_runtime.rs",
    "use crate::android_gamepad::{Devices, InputMode, Selection};\n",
    "use crate::android_gamepad::{Devices, Selection};\n"
    "pub use crate::android_gamepad::InputMode;\\n",
)

(root / "crates/horizon-client/src/android.rs").write_text(r'''use std::{path::PathBuf, sync::OnceLock};
use winit::platform::android::activity::AndroidApp;

static APP: OnceLock<AndroidApp> = OnceLock::new();

pub fn main(app: AndroidApp) {
    let private: PathBuf = app
        .internal_data_path()
        .unwrap_or_else(|| PathBuf::from("/data/local/tmp/pavohorizonnightly"));
    let _ = std::fs::create_dir_all(&private);
    let _ = std::env::set_current_dir(&private);
    let _ = APP.set(app.clone());
    winit::platform::android::set_input_hook(input::hook);
}

pub fn android_app() -> AndroidApp {
    APP.get()
        .expect("android_main must initialize AndroidApp before the game")
        .clone()
}

mod input {
    use horizon_client_render::android_runtime::{self, InputMode};
    use winit::event::{DeviceEvent, ElementState};
    use winit::platform::android::activity::{
        InputStatus,
        input::{Axis, InputEvent, KeyAction},
    };

    const SOURCE_GAMEPAD: u32 = 0x0000_0400;
    const SOURCE_JOYSTICK: u32 = 0x0100_0000;
    const SOURCE_TOUCHSCREEN: u32 = 0x0000_1000;
    const SOURCE_MOUSE: u32 = 0x0000_2000;

    fn is_gamepad(source: u32) -> bool {
        source & SOURCE_GAMEPAD != 0 || source & SOURCE_JOYSTICK != 0
    }

    pub(super) fn hook(event: &InputEvent<'_>) -> Option<InputStatus> {
        match event {
            InputEvent::KeyEvent(key) => {
                let source = u32::from(key.source());
                if is_gamepad(source) {
                    let state = match key.action() {
                        KeyAction::Down => ElementState::Pressed,
                        KeyAction::Up => ElementState::Released,
                        _ => return Some(InputStatus::Handled),
                    };
                    let event = DeviceEvent::Button {
                        button: u32::from(key.key_code()),
                        state,
                    };
                    android_runtime::gamepad_event(key.device_id(), &event);
                    return Some(InputStatus::Handled);
                }
                if matches!(key.action(), KeyAction::Down) {
                    android_runtime::note_input(InputMode::KeyboardMouse);
                }
                None
            }
            InputEvent::MotionEvent(motion) => {
                let source = u32::from(motion.source());
                if is_gamepad(source) {
                    if motion.pointer_count() > 0 {
                        let pointer = motion.pointer_at_index(0);
                        for (axis, kind) in [
                            (0, Axis::X),
                            (1, Axis::Y),
                            (11, Axis::Z),
                            (14, Axis::Rz),
                            (15, Axis::HatX),
                            (16, Axis::HatY),
                            (17, Axis::Ltrigger),
                            (18, Axis::Rtrigger),
                            (22, Axis::Gas),
                            (23, Axis::Brake),
                        ] {
                            let event = DeviceEvent::Motion {
                                axis,
                                value: pointer.axis_value(kind) as f64,
                            };
                            android_runtime::gamepad_event(motion.device_id(), &event);
                        }
                    }
                    return Some(InputStatus::Handled);
                }
                if source & SOURCE_TOUCHSCREEN != 0 {
                    android_runtime::note_input(InputMode::Touch);
                } else if source & SOURCE_MOUSE != 0 {
                    android_runtime::note_input(InputMode::KeyboardMouse);
                }
                None
            }
            _ => None,
        }
    }
}
''')

(root / "crates/horizon-client/src/android_lib.rs").write_text(r'''#![cfg(target_os = "android")]

use winit::platform::android::activity::AndroidApp;

#[unsafe(no_mangle)]
pub fn android_main(app: AndroidApp) {
    horizon_client::android::main(app);
}
''')

(root / "crates/horizon-android").mkdir(parents=True, exist_ok=True)
(root / "crates/horizon-android/Cargo.toml").write_text(r'''[package]
name = "horizon-android"
version = "0.1.0"
edition.workspace = true
license.workspace = true
rust-version.workspace = true
publish = false

[lib]
crate-type = ["cdylib"]
path = "../horizon-client/src/android_lib.rs"

[target.'cfg(target_os = "android")'.dependencies]
horizon-client = { path = "../horizon-client", default-features = false }
winit = { version = "0.30.13", features = ["android-native-activity"] }

[lints]
workspace = true
''')

replace_once(
    "crates/horizon-client/src/game/runtime/loading_screen.rs",
    "    let result = EventLoop::new()?.run_app(&mut split);\n",
    "    #[cfg(target_os = \"android\")]\n"
    "    let result = {\n"
    "        use winit::platform::android::EventLoopBuilderExtAndroid;\n"
    "        let mut builder = EventLoop::builder();\n"
    "        builder.with_android_app(crate::android::android_app());\n"
    "        builder.build()?.run_app(&mut split)\n"
    "    };\n"
    "    #[cfg(not(target_os = \"android\"))]\n"
    "    let result = EventLoop::new()?.run_app(&mut split);\n",
)

replace_once(
    "crates/horizon-client/src/game/runtime/no_resources.rs",
    "    EventLoop::new()?.run_app(&mut app)?;\n",
    "    #[cfg(target_os = \"android\")]\n"
    "    {\n"
    "        use winit::platform::android::EventLoopBuilderExtAndroid;\n"
    "        let mut builder = EventLoop::builder();\n"
    "        builder.with_android_app(crate::android::android_app());\n"
    "        builder.build()?.run_app(&mut app)?;\n"
    "    }\n"
    "    #[cfg(not(target_os = \"android\"))]\n"
    "    EventLoop::new()?.run_app(&mut app)?;\n",
)

replace_once(
    "crates/horizon-client/src/game/runtime/resource_loader.rs",
    "    let result = EventLoop::new()?.run_app(&mut app);\n",
    "    #[cfg(target_os = \"android\")]\n"
    "    let result = {\n"
    "        use winit::platform::android::EventLoopBuilderExtAndroid;\n"
    "        let mut builder = EventLoop::builder();\n"
    "        builder.with_android_app(crate::android::android_app());\n"
    "        builder.build()?.run_app(&mut app)\n"
    "    };\n"
    "    #[cfg(not(target_os = \"android\"))]\n"
    "    let result = EventLoop::new()?.run_app(&mut app);\n",
)

replace_once(
    "crates/horizon-client-input/Cargo.toml",
    'arboard = { version = "3.6.1", default-features = false, features = ["wayland-data-control"] }\n',
    "",
)
with (root / "crates/horizon-client-input/Cargo.toml").open("a") as file:
    file.write("""
[target.'cfg(not(target_os = "android"))'.dependencies]
arboard = { version = "3.6.1", default-features = false, features = ["wayland-data-control"] }
""")

replace_once(
    "crates/horizon-client-input/src/clipboard.rs",
    "    system: RefCell<Option<arboard::Clipboard>>,\n",
    "    #[cfg(not(target_os = \"android\"))]\n"
    "    system: RefCell<Option<arboard::Clipboard>>,\n",
)
replace_once(
    "crates/horizon-client-input/src/clipboard.rs",
    """    fn system(&self) -> RefMut<'_, Option<arboard::Clipboard>> {
""",
    """    #[cfg(not(target_os = "android"))]
    fn system(&self) -> RefMut<'_, Option<arboard::Clipboard>> {
""",
)
replace_once(
    "crates/horizon-client-input/src/clipboard.rs",
    """    pub fn text(&self) -> String {
        let mut system = self.system();
        let Some(board) = system.as_mut() else {
            return self.copied().unwrap_or_default();
        };
        match board.get_text() {
            Ok(text) => text,
            Err(arboard::Error::ContentNotAvailable) => String::new(),
            Err(_) => {
                // A lost connection (a restarted compositor): reconnect next time.
                *system = None;
                String::new()
            }
        }
    }
""",
    """    pub fn text(&self) -> String {
        #[cfg(target_os = "android")]
        {
            return self.copied().unwrap_or_default();
        }
        #[cfg(not(target_os = "android"))]
        {
            let mut system = self.system();
            let Some(board) = system.as_mut() else {
                return self.copied().unwrap_or_default();
            };
            match board.get_text() {
                Ok(text) => text,
                Err(arboard::Error::ContentNotAvailable) => String::new(),
                Err(_) => {
                    *system = None;
                    String::new()
                }
            }
        }
    }
""",
)
replace_once(
    "crates/horizon-client-input/src/clipboard.rs",
    """        let mut system = self.system();
        if let Some(board) = system.as_mut()
            && board.set_text(text.to_owned()).is_err()
        {
            *system = None;
        }
""",
    """        #[cfg(not(target_os = "android"))]
        {
            let mut system = self.system();
            if let Some(board) = system.as_mut()
                && board.set_text(text.to_owned()).is_err()
            {
                *system = None;
            }
        }
""",
)

replace_once(
    "crates/horizon-client-input/src/keybinds.rs",
    """pub fn learn_layout(event: &winit::event::KeyEvent) {
    use winit::keyboard::{Key, PhysicalKey};
    use winit::platform::modifier_supplement::KeyEventExtModifierSupplement;
    let PhysicalKey::Code(code) = event.physical_key else {
        return;
    };
    let Some(name) = key_name(code) else {
        return;
    };
    let learn = horizon_client_core::key_display::learn_layout;
    match event.key_without_modifiers() {
        Key::Character(text) => learn(name, &text),
        Key::Dead(Some(c)) => learn(name, c.encode_utf8(&mut [0; 4])),
        _ => {}
    }
}
""",
    """pub fn learn_layout(event: &winit::event::KeyEvent) {
    use winit::keyboard::PhysicalKey;
    let PhysicalKey::Code(code) = event.physical_key else {
        return;
    };
    let Some(name) = key_name(code) else {
        return;
    };
    let learn = horizon_client_core::key_display::learn_layout;
    #[cfg(target_os = "android")]
    match &event.logical_key {
        winit::keyboard::Key::Character(text) => learn(name, text),
        winit::keyboard::Key::Dead(Some(c)) => learn(name, c.encode_utf8(&mut [0; 4])),
        _ => {}
    }
    #[cfg(not(target_os = "android"))]
    {
        use winit::platform::modifier_supplement::KeyEventExtModifierSupplement;
        match event.key_without_modifiers() {
            winit::keyboard::Key::Character(text) => learn(name, &text),
            winit::keyboard::Key::Dead(Some(c)) => learn(name, c.encode_utf8(&mut [0; 4])),
            _ => {}
        }
    }
}
""",
)

replace_once(
    "crates/horizon-client-input/src/clipboard.rs",
    "use std::cell::{RefCell, RefMut};\n",
    "use std::cell::RefCell;\n"
    '#[cfg(not(target_os = "android"))]\n'
    "use std::cell::RefMut;\n",
)

replace_once(
    "crates/horizon-client-input/src/keybinds.rs",
    """pub fn raw_key_code(physical: winit::keyboard::PhysicalKey) -> u32 {
    use winit::keyboard::{NativeKeyCode, PhysicalKey};
    use winit::platform::scancode::PhysicalKeyExtScancode;
    match physical {
        PhysicalKey::Unidentified(native) => match native {
            NativeKeyCode::Android(c) | NativeKeyCode::Xkb(c) => c,
            NativeKeyCode::MacOS(c) | NativeKeyCode::Windows(c) => c.into(),
            NativeKeyCode::Unidentified => 0,
        },
        PhysicalKey::Code(_) => physical.to_scancode().unwrap_or(0),
    }
}
""",
    """pub fn raw_key_code(physical: winit::keyboard::PhysicalKey) -> u32 {
    use winit::keyboard::{NativeKeyCode, PhysicalKey};
    match physical {
        PhysicalKey::Unidentified(native) => match native {
            NativeKeyCode::Android(c) | NativeKeyCode::Xkb(c) => c,
            NativeKeyCode::MacOS(c) | NativeKeyCode::Windows(c) => c.into(),
            NativeKeyCode::Unidentified => 0,
        },
        #[cfg(target_os = "android")]
        PhysicalKey::Code(_) => 0,
        #[cfg(not(target_os = "android"))]
        PhysicalKey::Code(_) => {
            use winit::platform::scancode::PhysicalKeyExtScancode;
            physical.to_scancode().unwrap_or(0)
        }
    }
}
""",
)

replace_once(
    "crates/horizon-client/src/game/runtime/window_handler.rs",
    "            use winit::platform::modifier_supplement::KeyEventExtModifierSupplement;\n",
    "            #[cfg(not(target_os = \"android\"))]\n"
    "            use winit::platform::modifier_supplement::KeyEventExtModifierSupplement;\n",
)
replace_once(
    "crates/horizon-client/src/game/runtime/window_handler.rs",
    """            let layout_key = horizon_client::keybinds::layout_key_name(
                event.physical_key,
                &event.key_without_modifiers(),
            );
""",
    """            #[cfg(target_os = "android")]
            let layout_key = horizon_client::keybinds::layout_key_name(
                event.physical_key,
                &event.logical_key,
            );
            #[cfg(not(target_os = "android"))]
            let layout_key = horizon_client::keybinds::layout_key_name(
                event.physical_key,
                &event.key_without_modifiers(),
            );
""",
)

replace_once(
    "crates/horizon-mod-package/src/lib.rs",
    '    feature = "runtime-process",\n    any(target_os = "linux", target_os = "macos", windows)\n))]\npub mod holder;\n',
    '    feature = "runtime-process",\n    any(target_os = "linux", target_os = "macos", target_os = "android", windows)\n))]\npub mod holder;\n',
)

replace_once(
    "crates/horizon-mod-package/src/lib.rs",
    '    feature = "runtime-process",\n    any(target_os = "linux", target_os = "macos", windows)\n))]\npub mod local_control;\n',
    '    feature = "runtime-process",\n    any(target_os = "linux", target_os = "macos", target_os = "android", windows)\n))]\npub mod local_control;\n',
)

replace_once(
    "crates/horizon-android/Cargo.toml",
    'horizon-client = { path = "../horizon-client", default-features = false }\n',
    'horizon-client = { path = "../horizon-client", default-features = false }\n'
    'horizon-game = { path = "../horizon-game", default-features = false }\n'
    'horizon-official = { path = "../horizon-official" }\n',
)

replace_once(
    "crates/horizon-client/src/android_lib.rs",
    """pub fn android_main(app: AndroidApp) {
    horizon_client::android::main(app);
}
""",
    """pub fn android_main(app: AndroidApp) {
    horizon_client::android::main(app);
    horizon_official::register();
    horizon_game::main();
}
""",
)

replace_once(
    "crates/horizon-official/Cargo.toml",
    'horizon-game = { path = "../horizon-game" }\n',
    'horizon-game = { path = "../horizon-game", default-features = false }\n',
)

replace_once(
    "crates/horizon-mod-package/src/local_control/unix.rs",
    """    #[cfg(target_os = "linux")]
    let uid = rustix::net::sockopt::socket_peercred(stream)?.uid.as_raw();
""",
    """    #[cfg(any(target_os = "linux", target_os = "android"))]
    let uid = rustix::net::sockopt::socket_peercred(stream)?.uid.as_raw();
""",
)

replace_once(
    "crates/horizon-client/src/game/interface/pad/mod.rs",
    """        pub(super) fn read(&mut self) -> PadState {
            PadState::default()
        }""",
    """        pub(super) fn read(&mut self) -> PadState {
            #[cfg(target_os = "android")]
            {
                horizon_client_render::android_runtime::pad_state()
            }
            #[cfg(not(target_os = "android"))]
            {
                PadState::default()
            }
        }""",
)

print("Android bootstrap injected:")
for item in (
    "crates/horizon-android/Cargo.toml",
    "crates/horizon-client/src/android.rs",
    "crates/horizon-client/src/android_lib.rs",
    "crates/horizon-client/src/game/runtime/loading_screen.rs",
):
    print(item)

patch_winit(root)
