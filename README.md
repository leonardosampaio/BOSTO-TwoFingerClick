# TwoFingerClick

Adds **two-finger click = right click** to a Bosto USB touchpad on Windows.

The touchpad identifies itself as an Apple Magic Trackpad 2 (USB `VID_05AC PID_0265`). Windows does not enable Precision Touchpad mode for it, so it only works as a plain mouse: the cursor moves and you can left-click, but there is no right click with two fingers. TwoFingerClick is a small tray program that fixes that. It needs no driver, no test signing and no change to Secure Boot.

## What it does

- **Click with two fingers** (physically press the pad) → right click.
- **Tap with two fingers** → right click. You can turn this off with `--no-tap`.
- Everything else (cursor movement, one-finger click) still comes from the device's normal mouse behavior.

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

An icon appears in the system tray. Right-click it and choose **Sair** to quit. Only one instance runs at a time.

Options:

| Option     | Effect                                                       |
|------------|--------------------------------------------------------------|
| `--no-tap` | Turns off the two-finger tap (the two-finger click still works) |
| `--log`    | Writes `TwoFingerClick.log` next to the exe, for debugging   |

## Start with Windows

To start it at logon, put a shortcut to the exe in the Startup folder (`shell:startup`). From PowerShell:

```powershell
$s = (New-Object -ComObject WScript.Shell).CreateShortcut("$([Environment]::GetFolderPath('Startup'))\TwoFingerClick.lnk")
$s.TargetPath = "$PWD\publish\TwoFingerClick.exe"; $s.WorkingDirectory = "$PWD\publish"; $s.Save()
```

To uninstall, delete that shortcut and quit the program from the tray.

## Tuning

These constants at the top of `TouchWindow` in `Program.cs` control the timing and movement limits:

| Constant            | Default | Meaning                                                     |
|---------------------|---------|-------------------------------------------------------------|
| `TapMaxMs`          | 220     | A two-finger touch that lasts longer than this is not a tap |
| `TapMaxMove`        | 120     | Maximum finger movement for a tap, in touchpad units (about 0–4096) |
| `StaleMs`           | 250     | A finger count older than this is ignored                   |
| `SwallowAfterTapMs` | 150     | How long after a tap a left click from the device's firmware is discarded |
