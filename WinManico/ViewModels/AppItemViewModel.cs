using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using WinManico.Core;

namespace WinManico.ViewModels
{
    public partial class AppItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private ImageSource _icon;

        [ObservableProperty]
        private string _title;
        
        [ObservableProperty]
        private string _keyDisplay; // "1", "Q", etc.

        public List<WindowInfo> Windows { get; private set; }
        private readonly WindowManager _windowManager;

        public AppItemViewModel(List<WindowInfo> windows, string keyDisplay, ImageSource icon, WindowManager windowManager)
        {
            Windows = windows;
            // Use the title of the first window (most recent)
            Title = windows.FirstOrDefault()?.Title ?? "Unknown";
            KeyDisplay = keyDisplay;
            Icon = icon;
            _windowManager = windowManager;
        }

        public string? ExecutablePath { get; set; }

        [RelayCommand]
        public void Activate()
        {
            // Activate the first window in the group (Most Recent)
            var target = Windows.FirstOrDefault();
            if (target != null)
            {
                var handle = target.Handle;
                
                // Run on background thread
                Task.Run(() =>
                {
                    try
                    {
                        IntPtr foreground = NativeMethods.GetForegroundWindow();
                        
                        // SMART TOGGLE: If this app is ALREADY active, switch to the Previous App (Toggle Back)
                        if (Windows.Any(w => w.Handle == foreground))
                        {
                            Logger.Info($"[ACTIVATE] App is active. Toggling back to previous app.");
                            
                            // Find the first window in Z-Order that isn't part of this app
                            var allWindows = _windowManager.GetOpenWindows(); // Re-scan to get fresh Z-Order
                            var previousApp = allWindows.FirstOrDefault(w => !Windows.Any(my => my.Handle == w.Handle));
                            
                            if (previousApp != null)
                            {
                                Logger.Info($"[ACTIVATE] Toggling to: {previousApp.Title} ({previousApp.ProcessName})");
                                _windowManager.SwitchToWindow(previousApp.Handle);
                            }
                            else
                            {
                                Logger.Info($"[ACTIVATE] No previous app found to toggle to.");
                            }
                        }
                        else
                        {
                            // App not active -> Activate it
                            Logger.Info($"[ACTIVATE] Switching to app window {handle}");
                            _windowManager.SwitchToWindow(handle);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[ACTIVATE] ERROR switching: {ex.Message}");
                    }
                });
            }
            else if (!string.IsNullOrEmpty(ExecutablePath))
            {
                // No windows, but we have a launch path
                try
                {
                    Logger.Info($"[ACTIVATE] Launching from {ExecutablePath}");
                     System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ExecutablePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ACTIVATE] ERROR launching app: {ex.Message}");
                }
            }
        }
    }
}
