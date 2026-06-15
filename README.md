# Desktop Kitty

A lightweight Windows desktop mascot inspired by [Bongo Cat](https://bongo.cat/). Desktop Kitty sits on your desktop as a transparent, always-on-top overlay and reacts to global keyboard and mouse input with minimal CPU and memory usage.

## Features

- **Transparent overlay** — Borderless, draggable window with a transparent background
- **Global input detection** — Reacts to keyboard and mouse events even when other apps are focused
- **Keyboard-aware paws** — Left-hand keys trigger the left paw; right-hand keys trigger the right paw
- **Rapid typing** — Fast or alternating key presses show both paws
- **Mouse clicks** — Global mouse clicks show a dedicated sprite
- **Idle recovery** — Returns to the idle pose 120 ms after the last input
- **Click-through mode** — Let mouse events pass through the overlay to apps underneath
- **Always on top** — Stays visible above other windows (toggleable)
- **Persistent settings** — Remembers window position, scale, and toggles between sessions
- **System tray** — Show/hide, resize, toggle options, and exit from the notification area

## Requirements

- Windows 10 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (optional, for IDE development)

## Installation

### From source

1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/BongoCat2.git
   cd BongoCat2
   ```

2. Replace the placeholder sprites in `Assets/Cat/` with your own artwork (optional):
   - `idle.png`
   - `left_paw.png`
   - `right_paw.png`
   - `both_paws.png`
   - `mouse_click.png`

   All sprites should share the same dimensions for flicker-free switching.

3. Build and run (see [Build](#build) below).

Settings are stored at:

```
%AppData%\DesktopKitty\settings.json
```

## Build

### Visual Studio 2022

1. Open `BongoCat2.slnx` (or `BongoCat2.csproj`).
2. Set the configuration to **Release** or **Debug**.
3. Press **F5** to run, or **Ctrl+Shift+B** to build.

### Command line

```bash
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

The published executable and assets are copied to:

```
bin/Release/net8.0-windows/
```

To create a self-contained build:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## Screenshots

Example gallery:

![Desktop Kitty](Screenshots/SS2.png)

## Usage

| Action | How |
|--------|-----|
| Move overlay | Click and drag (click-through must be off) |
| Hide / show | System tray → **Show** / **Hide** |
| Resize | Tray → **Increase Size** / **Decrease Size** |
| Click-through | Tray → **Toggle Click Through** |
| Always on top | Tray → **Toggle Always On Top** |
| Reset counter | Right click → Reset |
| Exit | Tray → **Exit** |

### Keyboard layout

**Left paw:** `Q W E R T`, `A S D F G`, `Z X C V B`

**Right paw:** `Y U I O P`, `H J K L`, `N M`

## Performance goals

| Goal | Approach |
|------|----------|
| Near 0% CPU when idle | No render loop; UI updates only on hook events |
| Under ~30 MB RAM | Cached sprites loaded once at startup; no animation engine |
| Low latency reactions | Low-level `WH_KEYBOARD_LL` and `WH_MOUSE_LL` hooks |
| Smooth visuals | Pre-frozen `BitmapImage` instances swapped in place |

## Project structure

```
TinyBongo/
├── Assets/Cat/          # Sprite images
├── Models/
│   └── AppSettings.cs   # Persisted settings model
├── Services/
│   ├── InputHookService.cs
│   ├── NativeMethods.cs
│   ├── SettingsService.cs
│   └── TrayService.cs
├── MainWindow.xaml      # Overlay UI
├── App.xaml             # Application entry
└── README.md
```
