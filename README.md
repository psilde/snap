<div align="center">
  <img src=".github/assets/snap-lockup.svg" alt="Snap" width="280"/>
</div>

<div align="center">

![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-0d1117?style=for-the-badge)
![C%23](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)

</div>

<br/>

Screenshot tools either dump a flat, full-monitor capture that leaks whatever else is on screen, or force you into an editor to redact it after the fact. Snap runs as a background hotkey listener — `Ctrl+Shift+S` freezes the desktop into a selectable overlay, lets you drag one or more blur boxes over anything sensitive, and copies or saves the composited result in the same motion.

<br/>

<img src=".github/assets/section-architecture.svg" alt="How It Works" width="100%"/>

```mermaid
flowchart LR
    A[Ctrl+Shift+S] --> B[Freeze desktop into overlay]
    B --> C[Drag region to select]
    C --> D[Drag blur boxes over sensitive areas]
    D --> E{Copy or Save}
    E -->|Copy| F[Clipboard image]
    E -->|Save| G[PNG to disk]
```

<br/>

<img src=".github/assets/section-features.svg" alt="Core Features" width="100%"/>

- **Global hotkey capture** — `Ctrl+Shift+S` freezes the full virtual desktop from anywhere, no matter which app has focus
- **Freeform region select** — drag a rectangle across any monitor and redraw it as many times as needed before locking it in
- **Selective blur** — drop one or more blur boxes over anything to redact, each with an independently adjustable radius
- **Copy or save** — send the composited image straight to the clipboard, or write it to disk as a timestamped PNG
- **Tray-first design** — runs with no visible main window; New Capture and Settings are always one click away in the tray
- **Configurable save folder** — defaults to `Documents\screen`, with an option to auto-copy the saved file's path to the clipboard

<br/>

<img src=".github/assets/section-techstack.svg" alt="Tech Stack" width="100%"/>

| Technology | Purpose |
|---|---|
| .NET 8 + WPF | Desktop UI framework |
| Hardcodet.NotifyIcon.Wpf | System tray icon — WPF has no built-in support |
| System.Drawing (GDI) | Screen capture and blur compositing |
| Win32 `RegisterHotKey` (P/Invoke) | Global hotkey registration on a hidden message-only window |

<br/>

<img src=".github/assets/section-setup.svg" alt="Getting Started" width="100%"/>

```bash
# 1. Clone
git clone https://github.com/psilde/snap.git
cd snap

# 2. Run
dotnet run --project src/Snap
```

Settings are persisted as JSON at `%AppData%\ScreenBlur\settings.json`, created
automatically on first run with sensible defaults.

To publish a self-contained single-file `.exe`:

```bash
dotnet publish src/Snap -c Release -r win-x64
```
