use std::time::Duration;

use enigo::{Direction, Enigo, Key, Keyboard, Settings as EnigoSettings};
use tauri::menu::{Menu, MenuItem};
use tauri::tray::{MouseButton, TrayIconBuilder, TrayIconEvent};
use tauri::{AppHandle, Manager, WebviewWindow, WindowEvent};
use tauri_plugin_global_shortcut::{Code, GlobalShortcutExt, Modifiers, Shortcut, ShortcutState};

fn show_picker(app: &AppHandle) {
    if let Some(w) = app.get_webview_window("main") {
        let _ = w.show();
        let _ = w.set_focus();
    }
}

#[tauri::command]
fn hide_window(window: WebviewWindow) {
    let _ = window.hide();
}

#[tauri::command]
fn open_external(target: String) {
    let _ = open::that(target);
}

/// Hide the picker, let focus return to the previous app, then synthesize paste.
#[tauri::command]
fn paste_and_hide(app: AppHandle) {
    if let Some(w) = app.get_webview_window("main") {
        let _ = w.hide();
    }
    std::thread::sleep(Duration::from_millis(120));
    if let Ok(mut enigo) = Enigo::new(&EnigoSettings::default()) {
        #[cfg(target_os = "macos")]
        let modifier = Key::Meta;
        #[cfg(not(target_os = "macos"))]
        let modifier = Key::Control;
        let _ = enigo.key(modifier, Direction::Press);
        let _ = enigo.key(Key::Unicode('v'), Direction::Click);
        let _ = enigo.key(modifier, Direction::Release);
    }
}

/// Re-register the global hotkey from a chord string like "Alt+Shift+V".
#[tauri::command]
fn set_hotkey(app: AppHandle, chord: String) {
    let gs = app.global_shortcut();
    let _ = gs.unregister_all();
    if let Some(sc) = parse_chord(&chord) {
        let _ = gs.register(sc);
    }
}

fn parse_chord(s: &str) -> Option<Shortcut> {
    let mut mods = Modifiers::empty();
    let mut code: Option<Code> = None;
    for part in s.split('+') {
        match part.trim().to_ascii_lowercase().as_str() {
            "alt" | "option" | "opt" => mods |= Modifiers::ALT,
            "ctrl" | "control" => mods |= Modifiers::CONTROL,
            "shift" => mods |= Modifiers::SHIFT,
            "win" | "super" | "cmd" | "meta" => mods |= Modifiers::SUPER,
            key => code = key_to_code(key),
        }
    }
    code.map(|c| Shortcut::new(Some(mods), c))
}

fn key_to_code(k: &str) -> Option<Code> {
    Some(match k.to_ascii_uppercase().as_str() {
        "A" => Code::KeyA, "B" => Code::KeyB, "C" => Code::KeyC, "D" => Code::KeyD,
        "E" => Code::KeyE, "F" => Code::KeyF, "G" => Code::KeyG, "H" => Code::KeyH,
        "I" => Code::KeyI, "J" => Code::KeyJ, "K" => Code::KeyK, "L" => Code::KeyL,
        "M" => Code::KeyM, "N" => Code::KeyN, "O" => Code::KeyO, "P" => Code::KeyP,
        "Q" => Code::KeyQ, "R" => Code::KeyR, "S" => Code::KeyS, "T" => Code::KeyT,
        "U" => Code::KeyU, "V" => Code::KeyV, "W" => Code::KeyW, "X" => Code::KeyX,
        "Y" => Code::KeyY, "Z" => Code::KeyZ,
        "0" => Code::Digit0, "1" => Code::Digit1, "2" => Code::Digit2, "3" => Code::Digit3,
        "4" => Code::Digit4, "5" => Code::Digit5, "6" => Code::Digit6, "7" => Code::Digit7,
        "8" => Code::Digit8, "9" => Code::Digit9,
        "F1" => Code::F1, "F2" => Code::F2, "F3" => Code::F3, "F4" => Code::F4,
        "F5" => Code::F5, "F6" => Code::F6, "F7" => Code::F7, "F8" => Code::F8,
        "F9" => Code::F9, "F10" => Code::F10, "F11" => Code::F11, "F12" => Code::F12,
        _ => return None,
    })
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _argv, _cwd| {
            show_picker(app);
        }))
        .plugin(
            tauri_plugin_global_shortcut::Builder::new()
                .with_handler(|app, _shortcut, event| {
                    if event.state() == ShortcutState::Pressed {
                        // Capture-time focus return happens via the OS; just show.
                        show_picker(app);
                    }
                })
                .build(),
        )
        .invoke_handler(tauri::generate_handler![
            hide_window,
            open_external,
            paste_and_hide,
            set_hotkey
        ])
        .setup(|app| {
            // Tray icon with Show / Quit.
            let show_i = MenuItem::with_id(app, "show", "Show picker", true, None::<&str>)?;
            let quit_i = MenuItem::with_id(app, "quit", "Quit Clipwell", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&show_i, &quit_i])?;
            TrayIconBuilder::new()
                .icon(app.default_window_icon().unwrap().clone())
                .tooltip("Clipwell")
                .menu(&menu)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "show" => show_picker(app),
                    "quit" => app.exit(0),
                    _ => {}
                })
                .on_tray_icon_event(|tray, event| {
                    if let TrayIconEvent::Click { button: MouseButton::Left, .. } = event {
                        show_picker(tray.app_handle());
                    }
                })
                .build(app)?;

            // Default global hotkey (the frontend re-registers from settings via set_hotkey).
            if let Some(sc) = parse_chord("Alt+Shift+V") {
                let _ = app.global_shortcut().register(sc);
            }

            // Start hidden (the window is visible:false in tauri.conf). The picker is
            // summoned by the global hotkey or the tray — never popped up on launch,
            // so it can't sit on top of the user's work uninvited.
            Ok(())
        })
        .on_window_event(|window, event| {
            // Hide on blur (picker behavior). Disable with CLIPWELL_NO_AUTOHIDE.
            if let WindowEvent::Focused(false) = event {
                if std::env::var("CLIPWELL_NO_AUTOHIDE").is_err() {
                    let _ = window.hide();
                }
            }
        })
        .run(tauri::generate_context!())
        .expect("error while running Clipwell");
}
