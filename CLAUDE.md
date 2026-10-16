# TwoFingerClick

Windows tray helper (C#, .NET 8, WinForms) that turns a two-finger click or tap on a Bosto USB touchpad into a right click. It also has a cursor speed that applies only to the touchpad. The user-facing instructions are in [README.md](README.md); this file documents the internals and the device.

The user asked only for two-finger right click on top of the current behavior. Do not replace the driver or turn this into a kernel driver unless asked. A user-mode helper avoids test signing and Secure Boot changes, which the user would have to do themselves.

## The device

The touchpad identifies itself as an Apple Magic Trackpad 2, `USB\VID_05AC&PID_0265`, but it is a clone. Windows binds only inbox drivers (`input.inf`, `msmouse.inf`). It does not show the Precision Touchpad settings page. The Apple Boot Camp driver in the driver store (`applemtp64.inf`) does not list PID `0265`.

HID top-level collections (from `HidP_GetCaps`):

| Interface/collection | Usage           | Notes |
|----------------------|-----------------|-------|
| `MI_00&Col01`        | 01:02 mouse     | Report 2: buttons 1–3, X/Y. This is what moves the cursor. Windows opens it exclusively. |
| `MI_00&Col02`        | 0D:05 touch pad | Report 63, a 16-byte vendor blob (FF00:0C). Sent no data in tests. |
| `MI_00&Col03`        | FF00:0C vendor  | Report 68, 1388 bytes (Apple-style). Sent no data in tests. |
| `MI_01&Col01`        | 0D:05 touch pad | **Report 13 (0x0D), 30 bytes: the source of finger data.** Also has feature reports 14 (max contacts 0D:55, pad type 0D:59) and 15 (certification blob FF00:C5). |
| `MI_01&Col02`        | 0D:04 touch screen | Report 4, 10 contacts. Sent no data in tests. |
| `MI_01&Col03`        | FF02:00 vendor  | Reports 3/9–12. Raw Input refuses to register usage 0. |

`MI_01&Col01` has no Device Configuration collection (0D:0E, Input Mode), which Windows requires to enable PTP. That is probably why Windows falls back to mouse mode.

### Report 0x0D layout (`MI_01&Col01`)

```
[0]      report id = 0x0D
[1+5i]   contact i (i = 0..4): flags  bit0 confidence, bit1 tip, bits2-4 contact id
[2+5i]   X, little-endian u16 (observed ~0x700–0xA00, logical range ~0..4096)
[4+5i]   Y, little-endian u16
[26..27] scan time, u16
[28]     contact count
[29]     bit0 = physical button
```

It is sent about every 10 ms while fingers are down, and once with count 0 when they lift. Examples: one finger `0D-07-…-01-00`; two fingers `0D-07-…-0B-…-02-00`.

## How it works

Files: `Program.cs` (entry point, `TouchWindow`: raw input, hook, tray), `Settings.cs` (JSON settings), `OptionsForm.cs` (Options window), `TrayIcon.cs` (tray icon drawn at runtime).

- **Reading fingers:** `CreateFile`/`ReadFile` on the touchpad collections fails (error 5 or 32: Windows' input stack holds them). Instead, `RegisterRawInputDevices` registers 0D:05 with `RIDEV_INPUTSINK` and receives `WM_INPUT`. Reports are kept only when the device name contains `vid_05ac&pid_0265&mi_01` and the report id is `0x0D`.
- **Two-finger click:** a `WH_MOUSE_LL` hook watches non-injected `WM_LBUTTONDOWN`. If the last report shows ≥2 contacts and is less than `StaleMs` old, it swallows the left down/up and injects a right down/up with `SendInput`. Injected events (`LLMHF_INJECTED`) are ignored, so it never reacts to its own input.
- **Two-finger tap:** each touch session (count >0 back to 0) records the maximum contact count, duration, largest movement per contact id and whether the button was pressed. A session with 2 fingers, no press, at most `TapMaxMs` long and at most `TapMaxMove` of movement injects a right click. A firmware left click arriving within `SwallowAfterTapMs` afterwards is swallowed.
- **Touchpad-only cursor speed:** Windows has no per-device pointer speed, and the LL hook does not say which device a move came from. While the touchpad reports ≥1 contact (and the report is fresh), every non-injected `WM_MOUSEMOVE` is assumed to come from it. The hook takes the move Windows already computed (target − `GetCursorPos`, so system speed and acceleration are kept), multiplies it by `TouchpadSpeed`/100, keeps the fractional remainder, swallows the original and sends an absolute `SendInput` move (`MOUSEEVENTF_ABSOLUTE | VIRTUALDESK`). With no fingers on the pad, moves pass through untouched, so other mice are unaffected (unless used at the same moment).
- **Settings:** `Settings` is saved as `%APPDATA%\TwoFingerClick\settings.json` (`TapEnabled`, `TapMaxMs`, `TapMaxMove`, `StaleMs`, `SwallowAfterTapMs`, `TouchpadSpeed`). `TouchWindow` reads it on every event, so changes apply immediately.
- **Options window:** opened from the tray menu "Opções..." or by double-clicking the icon. Only one instance exists. Edits are applied live, OK saves, and Cancel/close restores the copy taken when the window opened. The test area paints itself on mouse down (blue = left, orange = right) and suppresses its context menu. `TouchWindow` raises `ContactsChanged` (synchronously, from raw input) and `Info` (posted with `BeginInvoke`, so the hook returns quickly). The window shows these as the finger count and the event list.
- **Tray icon:** `TrayIcon.Create()` draws a rounded pad with button split and two orange dots at `SmallIconSize`. It is white on a dark taskbar and dark gray on a light one (`SystemUsesLightTheme`). It is not redrawn when the theme changes until the program restarts.
- **Process:** a single-instance mutex `Local\TwoFingerClick`, a message-only `NativeWindow` and a `NotifyIcon` ("Opções...", "Sair"). `--log` writes `TwoFingerClick.log` next to the exe. `--no-tap` turns off the tap for that run.
- The hook runs on the UI thread and must return quickly: no I/O in it beyond the optional log line.

## Build and deploy

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

- Autostart: scheduled task `TwoFingerClick`, trigger at logon of the user, `LogonType Interactive`, `RunLevel Limited`, no time limit, `MultipleInstances IgnoreNew`, runs `publish\TwoFingerClick.exe` with no arguments. Registering it needs no administrator rights. A Startup folder shortcut was used first and was dropped: Windows started it about 4.5 minutes after logon.
- The exe is locked while it runs: quit it (tray → Sair, or `Stop-Process -Name TwoFingerClick`) before republishing.

## Possible next steps

- Two-finger scroll (send `MOUSEEVENTF_WHEEL`/`HWHEEL` from contact deltas) and 3/4-finger swipes (Win+Tab, Win+Ctrl+arrows, Win+D) from the same report.
- If the device is reconnected through the 2.4G receiver or Bluetooth, the VID/PID and interface may differ. Check with `Get-PnpDevice` and update `DeviceMatch`.
