# WinManico 🚀

**WinManico** is a fast, keyboard-centric application switcher for Windows, designed to bring the efficiency of macOS app switching to the PC.

## 🌟 Features

*   **⚡ Instant Switching**: Hold `Alt` to see your running apps, then press `1`, `2`, `Q`, `W`, etc. to switch instantly.
*   **🔄 Smart Toggle**: Pressing the hotkey for an already active app switches you to your **previous window** (simulating Alt-Tab), allowing for lightning-fast toggling between two tasks.
*   **🖥️ Virtual Desktop Support**: Seamlessly switches to windows even if they are on a different virtual desktop.
*   **🎯 Smart Deduplication**: Groups multiple processes (like Steam's many helpers) into a single, clean entry.
*   **⌨️ Native Pass-Through**: Only intercepts the keys you configure. `Alt+Tab`, `Alt+F4`, and `Alt+~` continue to work exactly as you expect.
*   **🛑 Game / App Blacklist**: Completely disables switching and Alt interception when configured apps (e.g. Diablo, full-screen games) are in the foreground, preventing macro and hotkey conflicts.
*   **⚙️ Fully Configurable**: Customize your hotkeys, blacklist, and preferences in Settings or `settings.json`.

## 📥 Download

Get the latest version from the **[Releases Page](https://github.com/luojiesi/WinManico/releases/latest)**.
*   **No installation required**: Just download the zip, extract, and run `WinManico.exe`.
*   *Note: Requires Windows 10/11 x64.*

## 💌 Inspiration & Credits

This project was heavily inspired by **[Manico](https://manico.im/)**, an amazing productivity utility for macOS.

I built WinManico because I missed that fluid, muscle-memory-driven workflow on Windows. If you use a Mac, I highly recommend checking out the original Manico! 🍎

## 🛠️ Configuration

Configure your apps and preferences in Settings or `settings.json`:

```json
{
  "AppConfigs": [
    { "ProcessName": "chrome", "ShortcutKey": "Q" },
    { "ProcessName": "steam", "ShortcutKey": "S" },
    { "ProcessName": "WindowsTerminal", "ShortcutKey": "1" }
  ],
  "Blacklist": [
    "Diablo IV",
    "D2R"
  ],
  "LogLevel": 2
}
```

### Logging Levels
*   `0`: None
*   `1`: Error
*   `2`: Info (Default)
*   `3`: Debug

## 🚀 Usage

1.  Build the solution using .NET 8.
2.  Run `WinManico.exe`.
3.  Hold generic `Alt` key to summon the dock.
4.  Press the corresponding number or letter for your app!

### 💡 Auto-Start
To enter the "set it and forget it" mode:
1.  Press `Win + R`, type `shell:startup`, and hit Enter.
2.  Create a shortcut to `WinManico.exe` in that folder.
3.  Now it starts with Windows!

## 📜 License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.
See the [LICENSE](LICENSE) file for details.

---
*Built with ❤️ for the Windows specific workflow.*
