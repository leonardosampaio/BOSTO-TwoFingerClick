# TwoFingerClick

Adds **two- and three-finger clicks and taps** (right click, middle click and more) and a **two-finger swipe for back/forward** in chosen apps to a Bosto USB touchpad on Windows.

The touchpad identifies itself as an Apple Magic Trackpad 2 (USB `VID_05AC PID_0265`). Windows does not enable Precision Touchpad mode for it, so it only works as a plain mouse: the cursor moves and you can left-click, but there is no right click with two fingers. TwoFingerClick is a small tray program that fixes that. It needs no driver, no test signing and no change to Secure Boot.

## What it does

- **Click with two fingers** (physically press the pad) → right click, by default.
- **Click with three fingers** → middle click, by default (e.g. close a Firefox tab, open a link in a new tab).
- **Tap with two fingers** → right click, by default. **Tap with three fingers** → nothing, by default.
- Each of these four gestures can be set in Options to left click, right click, middle click, back, forward or (for taps) nothing.
- **Swipe sideways with two fingers** → back (fingers to the right) or forward (to the left), only in the apps you tick in Options. Use it for apps that do not react to the swipe on their own. It is sent as the mouse back/forward buttons or as Alt+←/Alt+→.
- **Touchpad-only cursor speed:** 25–300% on top of the Windows pointer speed. Other mice are not affected.
- Everything else (one-finger click and the cursor movement itself) still comes from the device's normal mouse behavior.

## Requirements

- Windows 10/11 x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) to run it; the .NET 8 SDK to build it

## Build

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The output is `publish\TwoFingerClick.exe`.

## Run

```bash
publish\TwoFingerClick.exe
```

A touchpad icon appears in the system tray. Right-click it for **Opções...** (or double-click the icon) and **Sair** (quit). Only one instance runs at a time: starting it again while it runs opens the Options window of the running instance.

For a Start menu entry, create a shortcut in `%APPDATA%\Microsoft\Windows\Start Menu\Programs` pointing to the exe with `--options`. It starts the program if needed and opens Options.

### Options window

The window has three tabs.

**Cliques**

- **Ações:** the action for each gesture (two-finger click, three-finger click, two-finger tap, three-finger tap). For clicks, "Clique esquerdo (sem mudança)" lets the device's left click through unchanged. For taps, "Nada" turns the tap off.
- **Tempos:** the timing values (see [Tuning](#tuning)).
- **Cursor do touchpad:** cursor speed for the touchpad only, as a percentage.

**Deslizar**

- A list of the apps with open windows, each with a checkbox. Ticked apps get the two-finger back/forward swipe. Ticked apps stay in the list when they are closed, marked "(fechado)". **Atualizar lista** reloads the open apps.
- **Enviar como:** mouse back/forward buttons, or the keyboard shortcut Alt+←/Alt+→ for apps that only understand one of them.
- **Distância mínima:** how far the fingers must travel sideways to trigger, in touchpad units (the pad is about 4096 wide).
- **Inverter:** fingers to the right = forward.

**Teste**

- Shows how many fingers are on the pad, has an area that shows which button you clicked, and lists recent events (each touch's finger count, duration and movement, every click that was converted and every swipe with the app it went to). Use it to tune the values.

Changes take effect immediately so you can test them. **OK** saves them, **Cancelar** reverts them and **Restaurar padrões** fills in the defaults (the ticked apps are kept). Settings are stored in `%APPDATA%\TwoFingerClick\settings.json`.

Options:

| Option     | Effect                                                       |
|------------|--------------------------------------------------------------|
| `--no-tap` | Turns off the two- and three-finger taps for this run (overrides the saved setting) |
| `--options`| Opens the Options window on start                            |
| `--log`    | Writes `TwoFingerClick.log` next to the exe, for debugging (including the settings loaded at start) |

## Start with Windows

To start it at logon, register a scheduled task for your user (no administrator rights needed). From PowerShell, in the project folder:

```powershell
$exe = "$PWD\publish\TwoFingerClick.exe"; $user = "$env:USERDOMAIN\$env:USERNAME"
Register-ScheduledTask -TaskName TwoFingerClick -Force `
  -Action (New-ScheduledTaskAction -Execute $exe -WorkingDirectory (Split-Path $exe)) `
  -Trigger (New-ScheduledTaskTrigger -AtLogOn -User $user) `
  -Principal (New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited) `
  -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0 -MultipleInstances IgnoreNew)
```

A shortcut in the Startup folder (`shell:startup`) also works, but Windows can delay Startup folder programs by several minutes after logon.

Windows 11 puts new tray icons in the hidden overflow (the `^` arrow). To keep the icon visible, turn it on in Settings > Personalization > Taskbar > Other system tray icons.

To uninstall, run `Unregister-ScheduledTask -TaskName TwoFingerClick` and quit the program from the tray.

## Tuning

These values can be changed in the Options window:

| Setting (JSON name)                                       | Default | Meaning |
|-----------------------------------------------------------|---------|---------|
| Clique com 2 dedos (`TwoFingerClick`)                     | Right   | `Left` (unchanged), `Right`, `Middle`, `Back`, `Forward` |
| Clique com 3 dedos (`ThreeFingerClick`)                   | Middle  | Same choices |
| Toque com 2 dedos (`TwoFingerTap`)                        | Right   | Same choices plus `None` |
| Toque com 3 dedos (`ThreeFingerTap`)                      | None    | Same choices plus `None` |
| Duração máxima do toque (`TapMaxMs`)                      | 220 ms  | A touch that lasts longer than this is not a tap |
| Movimento máximo do toque (`TapMaxMove`)                  | 120     | Maximum finger movement for a tap, in touchpad units (about 0–4096) |
| Validade da contagem de dedos (`StaleMs`)                 | 250 ms  | A finger count older than this is ignored |
| Ignorar clique esquerdo após toque (`SwallowAfterTapMs`)  | 150 ms  | How long after a tap a left click from the device's firmware is discarded |
| Velocidade (`TouchpadSpeed`)                              | 100%    | Cursor speed when the movement comes from the touchpad |
| Apps marcados (`SwipeApps`)                               | none    | Process names (lowercase, no `.exe`) that get the swipe |
| Enviar como (`SwipeMethod`)                               | `MouseButtons` | `MouseButtons` (X1/X2) or `AltArrows` |
| Inverter (`SwipeInvert`)                                  | false   | true = fingers to the right go forward |
| Distância mínima (`SwipeMinUnits`)                        | 500     | Sideways travel that triggers the swipe |
