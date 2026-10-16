# TwoFingerClick

Adds **two-finger click = right click** to a Bosto USB touchpad on Windows.

The touchpad identifies itself as an Apple Magic Trackpad 2 (USB `VID_05AC PID_0265`). Windows does not enable Precision Touchpad mode for it, so it only works as a plain mouse: the cursor moves and you can left-click, but there is no right click with two fingers. TwoFingerClick is a small tray program that fixes that. It needs no driver, no test signing and no change to Secure Boot.

## What it does

- **Click with two fingers** (physically press the pad) → right click.
- **Tap with two fingers** → right click. You can turn this off in Options.
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

A touchpad icon appears in the system tray. Right-click it for **Opções...** (or double-click the icon) and **Sair** (quit). Only one instance runs at a time.

### Options window

- **Tempos:** turn the two-finger tap on or off and adjust the timing values (see [Tuning](#tuning)).
- **Cursor do touchpad:** cursor speed for the touchpad only, as a percentage.
- **Teste:** shows how many fingers are on the pad, has an area that shows which button you clicked, and lists recent events (each touch's finger count, duration and movement, and every click that was converted). Use it to tune the values.

Changes take effect immediately so you can test them. **OK** saves them, **Cancelar** reverts them and **Restaurar padrões** fills in the defaults. Settings are stored in `%APPDATA%\TwoFingerClick\settings.json`.

Options:

| Option     | Effect                                                       |
|------------|--------------------------------------------------------------|
| `--no-tap` | Turns off the two-finger tap for this run (overrides the saved setting) |
| `--log`    | Writes `TwoFingerClick.log` next to the exe, for debugging   |

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
| Duração máxima do toque (`TapMaxMs`)                      | 220 ms  | A two-finger touch that lasts longer than this is not a tap |
| Movimento máximo do toque (`TapMaxMove`)                  | 120     | Maximum finger movement for a tap, in touchpad units (about 0–4096) |
| Validade da contagem de dedos (`StaleMs`)                 | 250 ms  | A finger count older than this is ignored |
| Ignorar clique esquerdo após toque (`SwallowAfterTapMs`)  | 150 ms  | How long after a tap a left click from the device's firmware is discarded |
| Velocidade (`TouchpadSpeed`)                              | 100%    | Cursor speed when the movement comes from the touchpad |
