#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()

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
    "crates/horizon-client/src/lib.rs",
    "extern crate self as horizon_client;\n",
    "extern crate self as horizon_client;\n"
    '#[cfg(target_os = "android")]\n'
    "pub mod android;\n",
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
    let _ = APP.set(app);
}

pub fn android_app() -> AndroidApp {
    APP.get()
        .expect("android_main must initialize AndroidApp before the game")
        .clone()
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
    "crates/horizon-mod-package/src/local_control/unix.rs",
    """    #[cfg(target_os = "linux")]
    let uid = rustix::net::sockopt::socket_peercred(stream)?.uid.as_raw();
""",
    """    #[cfg(any(target_os = "linux", target_os = "android"))]
    let uid = rustix::net::sockopt::socket_peercred(stream)?.uid.as_raw();
""",
)

print("Android bootstrap injected:")
for item in (
    "crates/horizon-android/Cargo.toml",
    "crates/horizon-client/src/android.rs",
    "crates/horizon-client/src/android_lib.rs",
    "crates/horizon-client/src/game/runtime/loading_screen.rs",
):
    print(item)
