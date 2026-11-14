#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(sys.argv[1]).resolve()

def replace(path, old, new, expected=1):
    file = ROOT / path
    source = file.read_text(encoding="utf-8")
    count = source.count(old)
    if count != expected:
        raise SystemExit(f"{path}: expected {expected} exact match(es), found {count}")
    file.write_text(source.replace(old, new), encoding="utf-8")

# Android label: keep applicationId/package unchanged so existing data and
# update-signing identity are preserved.
replace(
    "packaging/android/AndroidManifest.xml",
    'android:label="Horizon Android"',
    'android:label="PavoHorizon"',
)

# Ask once from the app startup screen, not from the world lifecycle/frame.
# NativeActivity's super.onCreate initializes the native window first; the
# posted callback runs later on the UI thread while the player is still in the
# launcher/menu. The game remains usable if the user denies the permission.
replace(
    "packaging/android/java/dev/horizon/android/HorizonActivity.java",
    "    private native void nativeDocumentResult(int request, int result, String uri);\n",
    """    private native void nativeDocumentResult(int request, int result, String uri);

    @Override protected void onCreate(android.os.Bundle state) {
        super.onCreate(state);
        new android.os.Handler(android.os.Looper.getMainLooper())
                .postDelayed(this::requestAudioPermission, 1200);
    }
""",
)
replace(
    "packaging/android/java/dev/horizon/android/HorizonActivity.java",
    """    public void requestAudioPermission() {
        new android.os.Handler(android.os.Looper.getMainLooper()).postDelayed(() -> {
            if (android.os.Build.VERSION.SDK_INT >= 23 &&
                    checkSelfPermission(android.Manifest.permission.RECORD_AUDIO)
                            != android.content.pm.PackageManager.PERMISSION_GRANTED) {
                requestPermissions(
                        new String[]{android.Manifest.permission.RECORD_AUDIO}, 4102);
            }
        }, 500);
    }
""",
    """    public void requestAudioPermission() {
        if (android.os.Build.VERSION.SDK_INT < 23 || isFinishing() ||
                checkSelfPermission(android.Manifest.permission.RECORD_AUDIO)
                        == android.content.pm.PackageManager.PERMISSION_GRANTED) {
            return;
        }
        requestPermissions(
                new String[]{android.Manifest.permission.RECORD_AUDIO}, 4102);
    }

    @Override public void onRequestPermissionsResult(
            int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == 4102) {
            boolean granted = grantResults.length > 0 &&
                    grantResults[0] == android.content.pm.PackageManager.PERMISSION_GRANTED;
            android.util.Log.i("PavoHorizon",
                    granted ? "Microphone permission granted" : "Microphone permission denied");
        }
    }
""",
)

# Remove the upstream migration advertisement/download from the Android
# launcher. Keep the world-export action, which is useful independently.
replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    """    StarlingDownload,
""",
    "",
)
replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    """                ui.text(
                    Face::SemiBold,
                    15.0,
                    content_x + 22.0,
                    card_y + 84.0,
                    my3_text(),
                    "Horizon Android is becoming Starling",
                );
                let notice = "Some Horizon Android features are still being transferred and merged. Export your worlds through Android Files (SAF) before moving.";
                let mut line_y = card_y + 108.0;
                let font_size = 11.5;
                let mut line = String::new();
                for word in notice.split_whitespace() {
                    let candidate = if line.is_empty() {
                        word.to_owned()
                    } else {
                        format!("{line} {word}")
                    };
                    if !line.is_empty()
                        && ui.measure(Face::Regular, font_size, &candidate) > content_w - 44.0
                    {
                        ui.text(
                            Face::Regular,
                            font_size,
                            content_x + 22.0,
                            line_y,
                            my3_muted(),
                            &line,
                        );
                        line_y += 15.0;
                        line.clear();
                    }
                    if !line.is_empty() {
                        line.push(' ');
                    }
                    line.push_str(word);
                }
                if !line.is_empty() {
                    ui.text(
                        Face::Regular,
                        font_size,
                        content_x + 22.0,
                        line_y,
                        my3_muted(),
                        &line,
                    );
                }
                let migration_y = line_y + 24.0;
                let migration_w = (content_w - 52.0) * 0.5;
                button(
                    &mut ui,
                    &mut self.targets,
                    [content_x + 22.0, migration_y, migration_w, 34.0],
                    "Download Starling",
                    Action::StarlingDownload,
                    false,
                );
                button(
                    &mut ui,
                    &mut self.targets,
                    [
                        content_x + 30.0 + migration_w,
                        migration_y,
                        migration_w,
                        34.0,
                    ],
                    "Export worlds (SAF)",
                    Action::WorldFiles,
                    false,
                );
""",
    """                ui.text(
                    Face::SemiBold,
                    15.0,
                    content_x + 22.0,
                    card_y + 84.0,
                    my3_text(),
                    "PavoHorizon",
                );
                button(
                    &mut ui,
                    &mut self.targets,
                    [content_x + 22.0, card_y + 112.0, content_w - 44.0, 34.0],
                    "Export worlds (SAF)",
                    Action::WorldFiles,
                    false,
                );
""",
)
replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    """            Some(Action::StarlingDownload) => {
                if !crate::platform::open_url(
                    "https://github.com/JuanLuci/starling/releases/latest",
                ) {
                    self.catalog.status =
                        "Open github.com/JuanLuci/starling/releases to download Starling.".into();
                }
            }
""",
    "",
)
replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    "Keep Horizon Android installed until you verify your worlds in Starling.",
    "Keep PavoHorizon installed until you verify your exported worlds.",
)
replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    "Open system Files > Horizon Android.",
    "Open system Files > PavoHorizon.",
)
print("Applied PavoHorizon app label, startup microphone permission and launcher branding.")
