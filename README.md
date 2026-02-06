# WinManico 🚀

**WinManico** is a fast, keyboard-centric application switcher for Windows, designed to bring the efficiency of macOS app switching to the PC.

## 🌟 Features

*   **⚡ Instant Switching**: Hold `Alt` to see your running apps, then press `1`, `2`, `Q`, `W`, etc. to switch instantly.
*   **🔄 Smart Toggle**: Pressing the hotkey for an already active app switches you to your **previous window** (simulating Alt-Tab), allowing for lightning-fast toggling between two tasks.
*   **🖥️ Virtual Desktop Support**: Seamlessly switches to windows even if they are on a different virtual desktop.
*   **🎯 Smart Deduplication**: Groups multiple processes (like Steam's many helpers) into a single, clean entry.
*   **⌨️ Native Pass-Through**: Only intercepts the keys you configure. `Alt+Tab`, `Alt+F4`, and `Alt+~` continue to work exactly as you expect.
*   **⚙️ Fully Configurable**: Customize your hotkeys and preferred apps in `settings.json`.

## 💌 Inspiration & Credits

This project was heavily inspired by **[Manico](https://manico.im/)**, an amazing productivity utility for macOS.

I built WinManico because I missed that fluid, muscle-memory-driven workflow on Windows. If you use a Mac, I highly recommend checking out the original Manico! 🍎

## 🛠️ Configuration

Configure your apps and preferences in `settings.json`:

```json
{
  "AppConfigs": [
    { "ProcessName": "chrome", "ShortcutKey": "Q" },
    { "ProcessName": "steam", "ShortcutKey": "S" },
    { "ProcessName": "WindowsTerminal", "ShortcutKey": "1" }
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

## 📜 License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.
See the [LICENSE](LICENSE) file for details.

---
*Built with ❤️ for the Windows specific workflow.*
