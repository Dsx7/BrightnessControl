# Brightness Control

A lightweight, single-executable (`BrightnessControl.exe`) Windows system tray utility for smooth **0–100%** screen brightness control — no external DLLs or installers required.

**Author:** Al Helal Mohammod Bayijid  
**Repository:** [https://github.com/Dsx7/BrightnessControl](https://github.com/Dsx7/BrightnessControl)  
**Download:** Grab the latest standalone `BrightnessControl.exe` from the [Releases](https://github.com/Dsx7/BrightnessControl/releases/latest) page.

---

## Features

- **Single Standalone `.exe`**: Everything is compiled into `BrightnessControl.exe` (zero companion `.dll` files, zero `.ini` clutter — settings are saved cleanly in `HKCU\Software\BrightnessControl`).
- **Smooth 0–100% Draggable Slider**:
  - **Left-click tray icon**: Opens a theme-aware floating flyout right above the taskbar with a pixel-smooth 0–100% slider, `-` / `+` fine adjustment, clickable exact percentage input, quick preset chips, screen-off button, and inline setup panel.
  - **Right-click tray icon**: Context menu with an **embedded live 0–100% draggable slider**, Quick Presets submenu, Turn off screen, Start with Windows, Setup, and Exit.
- **Scroll Wheel on Tray Icon**: Hover over the tray icon and scroll the mouse wheel to adjust brightness by your configured step (`1%`–`25%`).
- **Customizable Setup**:
  - Edit quick preset percentages (e.g. `0, 25, 50, 75, 100` or custom values).
  - Configure tray mouse-wheel step size.
  - Toggle **Start with Windows**.
- **Supports Both Laptop & External Monitors**:
  - Uses **DDC/CI (`dxva2.dll`)** for external monitors and **WMI (`WmiMonitorBrightnessMethods`)** for laptop panels, with cached handles and a coalesced background worker for lag-free dragging.
- **Windows Light & Dark Theme Aware**: Automatically adapts the tray icon, popup flyout, and context menu to your Windows taskbar theme.

---

## Build From Source

Requires Windows with .NET Framework 4.x (`csc.exe` is included with Windows by default):

```powershell
.\src\build.ps1
```

This compiles `BrightnessControl.exe` directly into the repository root with all icons embedded.

