using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinManico.Core;

namespace WinManico.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private ObservableCollection<AppItemViewModel> _apps = new();

        private readonly WindowManager _windowManager;
        private readonly KeyboardHook _keyboardHook;
        private Settings _settings;
        private bool _isRefreshed = false;
        private System.Windows.Threading.DispatcherTimer _failsafeTimer;
        private System.Windows.Threading.DispatcherTimer _visibilityTimer; // Delayed visibility

        private bool _isEnabled = true; // Global toggle state

        public MainViewModel()
        {
            _settings = Settings.Load();
            _windowManager = new WindowManager();
            // KeyboardHook now needs settings to know what to intercept
            _keyboardHook = new KeyboardHook(_settings);

            _keyboardHook.AltKeyChanged += OnAltKeyChanged;
            _keyboardHook.KeyPressed += OnKeyPressed;
            _keyboardHook.DoubleAltDetected += OnDoubleAltDetected;
            _keyboardHook.AltSessionCancelled += OnAltSessionCancelled;
            
            // Failsafe Timer: Poll key state to ensure UI doesn't get stuck
            _failsafeTimer = new System.Windows.Threading.DispatcherTimer();
            _failsafeTimer.Interval = TimeSpan.FromMilliseconds(200);
            _failsafeTimer.Tick += FailsafeCheck;
            _failsafeTimer.Start();

            // Delayed Visibility Timer (500ms)
            _visibilityTimer = new System.Windows.Threading.DispatcherTimer();
            _visibilityTimer.Interval = TimeSpan.FromMilliseconds(500); 
            _visibilityTimer.Tick += (s, e) => 
            {
                // REDUNDANCY CHECK: If modifiers are down, DO NOT show
                if (AreModifiersDown()) 
                {
                    Logger.Debug("[VISIBILITY] Timer tick but modifiers down -> abort show");
                    _visibilityTimer.Stop();
                    return;
                }

                IsVisible = true;
                _visibilityTimer.Stop(); 
            };
        }

        private bool AreModifiersDown()
        {
             bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
             bool isShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
             bool isWin = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 || 
                          (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;
             return isCtrl || isShift || isWin;
        }

        private void OnAltSessionCancelled(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                 Logger.Info("[CANCEL] Alt session cancelled by modifier -> Hiding");
                 _visibilityTimer.Stop();
                 IsVisible = false;
            });
        }

        private void OnDoubleAltDetected(object sender, EventArgs e)
        {
            _isEnabled = !_isEnabled;
            Logger.Info($"[TOGGLE] App Enabled State: {_isEnabled}");
            
            // Notification
            (System.Windows.Application.Current as App)?.ShowNotification("WinManico", 
                _isEnabled ? "App Enabled" : "App Disabled", 
                System.Windows.Forms.ToolTipIcon.Info);

            // Visual/Audio feedback
            if (_isEnabled)
            {
                 System.Media.SystemSounds.Exclamation.Play(); // Enable sound
            }
            else
            {
                 System.Media.SystemSounds.Hand.Play(); // Disable sound
            }
        }

        private void FailsafeCheck(object sender, EventArgs e)
        {
            // If we think Alt is DOWN or UI is visible, double-check reality
            if (_isRefreshed || IsVisible)
            {
                // Check physical key state of Left Alt (VK_LMENU = 0xA4)
                short state = NativeMethods.GetAsyncKeyState(0xA4);
                bool isActuallyDown = (state & 0x8000) != 0;

                Logger.Debug($"[FAILSAFE] Checking: IsVisible={IsVisible}, _isRefreshed={_isRefreshed}, AltPhysicallyDown={isActuallyDown}");

                if (!isActuallyDown)
                {
                    // Force cleanup
                    Logger.Debug("[FAILSAFE] *** FORCING UI CLOSE - Alt released but event missed ***");
                    IsVisible = false;
                    _isRefreshed = false;
                }
            }
        }

        private void OnAltKeyChanged(object sender, bool isDown)
        {
            if (!_isEnabled && isDown) 
            {
                Logger.Debug("[DISABLED] Alt ignored because app is disabled.");
                return;
            }

            Logger.Debug($"[ALT EVENT] Alt key changed: isDown={isDown}");
            
            // CRITICAL: Use InvokeAsync to avoid blocking the keyboard hook
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Logger.Debug($"[DISPATCHER] Processing Alt event: isDown={isDown}, CurrentVisible={IsVisible}, CurrentRefreshed={_isRefreshed}");
                
                if (isDown)
                {
                    // Prepare UI immediately (refresh data) but DELAY showing it
                    if (!_isRefreshed)
                    {
                        _isRefreshed = true;
                        
                        // Refresh apps every time Alt is pressed to detect new windows
                        Logger.Debug("[REFRESH] Refreshing app list...");
                        RefreshApps();
                        Logger.Debug($"[REFRESH] Apps refreshed, count={Apps.Count}");
                        
                        // Start timer to show window eventually
                        _visibilityTimer.Start();
                    }
                }
                else
                {
                    Logger.Debug("[VISIBILITY] Setting IsVisible=FALSE (Alt released)");
                    _visibilityTimer.Stop(); // Cancel if released early
                    IsVisible = false;
                    _isRefreshed = false; // Reset for next Alt press
                }
            });
        }

        private void OnKeyPressed(object sender, string key, ref bool handled)
        {
            // Allow processing if the session is active (Alt is held)
            if (_isRefreshed)
            {
                // Normal Key Handling
                var app = Apps.FirstOrDefault(a => a.KeyDisplay.Equals(key, StringComparison.OrdinalIgnoreCase));
                
                if (app != null)
                {
                    handled = true; // Swallow key
                    
                    // Dispatch activation
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Stop visibility timer immediately to prevent late popup
                        _visibilityTimer.Stop();
                        
                        app.Activate();
                        IsVisible = false; 
                    });
                }
                else
                {
                    // App not in current list - check if configured and launch if needed
                    var config = _settings.AppConfigs.FirstOrDefault(c => 
                        c.ShortcutKey.Equals(key, StringComparison.OrdinalIgnoreCase));
                    
                    if (config != null && !string.IsNullOrEmpty(config.ExecutablePath))
                    {
                        handled = true;
                        
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = config.ExecutablePath,
                                UseShellExecute = true
                            });
                            
                            _visibilityTimer.Stop();
                            IsVisible = false;
                        }
                        catch (Exception ex)
                        {
                            // Log error
                        }
                    }
                }
            }
        }

        private void RefreshApps()
        {
            // RELOAD SETTINGS to ensure we have the latest config (e.g. after user edits)
            _settings = Settings.Load();
            
            Apps.Clear();
            var windows = _windowManager.GetOpenWindows();
            
            // WHITELIST MODE: Filter windows to only show configured processes
            if (_settings.WhitelistMode)
            {
                var configuredProcesses = _settings.AppConfigs
                    .Select(c => c.ProcessName.ToLowerInvariant())
                    .ToHashSet();
                    
                windows = windows.Where(w => 
                    configuredProcesses.Contains(w.ProcessName.ToLowerInvariant()))
                    .ToList();
            }
            
            // Group by Process Name (Deduplication)
            var groups = windows.GroupBy(w => w.ProcessName.ToLowerInvariant()).ToList();
            
            // Helper to find group
            IGrouping<string, WindowInfo> FindGroup(string procName) 
            {
                return groups.FirstOrDefault(g => g.Key.Equals(procName, StringComparison.OrdinalIgnoreCase));
            }

            var usedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Configured Apps (In Settings Order)
            // This ensures stability: Configured apps always appear in the sequence defined in settings.json
            foreach (var config in _settings.AppConfigs)
            {
                // SMART MATCHING: Find ALL groups that start with the config name (e.g. "steam" matches "steam" and "steamwebhelper")
                // This handles apps that spawn multiple helper processes with similar names.
                var matchingGroups = groups.Where(g => 
                    g.Key.StartsWith(config.ProcessName, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchingGroups.Any())
                {
                    // Merge all windows from all matching groups
                    var allWindows = matchingGroups.SelectMany(g => g).ToList();
                    
                    // Create VM
                    var vm = CreateAppVM(allWindows, config.ShortcutKey);
                    vm.ExecutablePath = config.ExecutablePath;
                    Apps.Add(vm); // Add directly to preserve order
                    
                    // Mark ALL matched process names as used
                    foreach (var mg in matchingGroups)
                    {
                        usedGroups.Add(mg.Key);
                    }
                }
                else
                {
                    // Not Running -> Launcher
                    var icon = ExtractIcon(config.ExecutablePath, config.ProcessName, config.ProcessName);
                    var vm = new AppItemViewModel(new List<WindowInfo>(), config.ShortcutKey, icon, _windowManager);
                    vm.Title = config.ProcessName;
                    vm.ExecutablePath = config.ExecutablePath;
                    Apps.Add(vm); // Add directly
                    usedGroups.Add(config.ProcessName);
                }
            }

            // 2. Unassigned Apps (Sorted Alphabetically)
            // This ensures stability: The remaining apps won't jump around based on Z-Order
            var unassigned = groups
                .Where(g => !usedGroups.Contains(g.Key))
                .OrderBy(g => g.Key) // Sort A-Z by process name
                .ToList();

            int number = 1;
            foreach (var group in unassigned)
            {
                if (number > 9) break;

                // Create VM with Number key
                var vm = CreateAppVM(group.ToList(), number.ToString());
                Apps.Add(vm);
                number++;
            }
            
            // Apps collection is now populated in stable order!
        }

        private AppItemViewModel CreateAppVM(List<WindowInfo> windows, string key)
        {
            var firstWindow = windows.First();
            // Use the icon of the first window
            var icon = ExtractIcon(firstWindow.ProcessPath, firstWindow.ProcessName, firstWindow.Title);
            return new AppItemViewModel(windows, key, icon, _windowManager);
        }

        private ImageSource ExtractIcon(string path, string processName, string title)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    // Try to find the executable in System32 for common Windows processes
                    var systemPath = TryGetSystemPath(processName);
                    if (!string.IsNullOrEmpty(systemPath))
                    {
                        Logger.Debug($"[ICON] Trying system path for {processName}: {systemPath}");
                        path = systemPath;
                    }
                    else
                    {
                        return FallbackIconProvider.GetFallbackIcon(processName, title);
                    }
                }

                NativeMethods.SHFILEINFO shinfo = new NativeMethods.SHFILEINFO();
                NativeMethods.SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
                
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    var icon = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    // Cleanup unmanaged resource
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                    
                    return icon;
                }
            }
            catch 
            { 
                Logger.Debug($"[ICON] Failed to extract icon for {processName}, using fallback");
            }
            
            // Return fallback icon if extraction failed (likely elevated process)
            return FallbackIconProvider.GetFallbackIcon(processName, title);
        }

        private string TryGetSystemPath(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return null;

            // Common system processes that live in System32
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var normalizedName = processName.ToLower();
            
            // Try exact process name first
            var systemPath = Path.Combine(systemDir, processName);
            if (File.Exists(systemPath))
                return systemPath;
            
            // Try with .exe extension if not present
            if (!normalizedName.EndsWith(".exe"))
            {
                systemPath = Path.Combine(systemDir, processName + ".exe");
                if (File.Exists(systemPath))
                    return systemPath;
            }
            
            return null;
        }
    }
}
