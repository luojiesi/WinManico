using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Media;

namespace WinManico.Core
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        // We will load the actual Icon ImageSource in the ViewModel to keep this model light
        public string ProcessPath { get; set; }
    }

    public class WindowManager
    {
        private IntPtr _previousWindow = IntPtr.Zero;
        private IntPtr _lastTargetWindow = IntPtr.Zero;  // Track what we're switching TO
        private readonly object _switchLock = new object();  // Prevent concurrent switches

        // Common system windows that shouldn't appear in the switcher
        private static readonly HashSet<string> IgnoredTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft Text Input Application",
            "Search",
            "Start", 
            "Cortana",
            "People",
            "NVIDIA GeForce Overlay",
            "Settings" // Windows Settings is usually fine, but 'Settings' often catches 'Settings' background process. Let's keep it visible generally, but if user complains we add it here.
                       // Removing "Settings" from ignore for now, often useful.
        };
        public List<WindowInfo> GetOpenWindows()
        {
            var windows = new List<WindowInfo>();
            var shellWindow = NativeMethods.GetShellWindow();
            int myProcessId = Process.GetCurrentProcess().Id;

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == shellWindow) return true;
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                // Check DWM Cloaked State (e.g. Metro apps in background, Input Experience)
                if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0)
                {
                    // Debug Logging for Virtual Desktop Issue
                    // DWM_CLOAKED_APP (1) = Suspended UWP.
                    // DWM_CLOAKED_SHELL (2) = Other Desktop / Immersive Shell
                    
                    if (cloaked != 0)
                    {
                         // Console.WriteLine($"[DEBUG-WIN] Handle {hWnd}, Cloaked={cloaked} ({cloaked:X})");
                    }

                    if ((cloaked & NativeMethods.DWM_CLOAKED_APP) != 0) 
                    {
                        Logger.Debug($"[DEBUG-WIN] Skipping Cloaked APP: Handle={hWnd}, Cloaked={cloaked}");
                        return true; 
                    }
                    
                    if ((cloaked & NativeMethods.DWM_CLOAKED_SHELL) != 0)
                    {
                        // Log specifically when we ALLOW a Shell/Desktop cloaked window
                         Logger.Debug($"[DEBUG-WIN] Allowing Cloaked SHELL (Other Desktop?): Handle={hWnd}, Cloaked={cloaked}");
                    }
                }

                // Check for ToolWindow style (hide them)
                int exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                // Skip Program Manager
                if (title == "Program Manager") return true;

                // Known System Phantom/Noise Windows
                if (IgnoredTitles.Contains(title) || title.StartsWith("Windows Input Experience", StringComparison.OrdinalIgnoreCase)) 
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                
                // Skip Self
                if (processId == myProcessId) return true;

                try
                {
                    Process p = Process.GetProcessById((int)processId);
                    
                    // Console.WriteLine($"Found Candidate: {title} ({p.ProcessName})");

                    windows.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        ProcessId = (int)processId,
                        ProcessName = p.ProcessName,
                        ProcessPath = GetProcessPath(p)
                    });
                }
                catch (Exception)
                {
                    // Process might have exited or Access Denied
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        private string GetProcessPath(Process p)
        {
            try 
            { 
                return p.MainModule?.FileName; 
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access Denied (system/elevated process).
                // We could try QueryFullProcessImageName via PInvoke here, 
                // but for now let's just return null and handle missing icon in UI.
                return null;
            }
            catch 
            { 
                return null; 
            }
        }


        public void SwitchToWindow(IntPtr hWnd)
        {
            lock (_switchLock)
            {
                // Correct Toggle Logic:
                // Only toggle if the requested window IS ALREADY the Foreground Window.
                // It doesn't matter if we targeted it last time. If the user clicked away, we want to bring it BACK, not toggle away.
                IntPtr currentForeground = NativeMethods.GetForegroundWindow();

                if (currentForeground == hWnd && _previousWindow != IntPtr.Zero && _previousWindow != hWnd)
                {
                    // It IS active. Toggle back to the previous one.
                    Logger.Debug($"[SWITCH] Toggling BACK: {hWnd} -> {_previousWindow}");
                    IntPtr temp = _previousWindow;
                    // We keep _lastTargetWindow as hWnd because we are technically leaving it, 
                    // but semantic toggle means we are "visiting" previous.
                    // Actually, if we toggle back to B, B becomes the new "Last Target".
                    _previousWindow = hWnd; 
                    _lastTargetWindow = temp;
                    hWnd = temp;
                }
                else
                {
                    // It is NOT active (or we have no history). Bring it to front.
                    Logger.Debug($"[SWITCH] Switching TO {hWnd} (Current: {currentForeground})");
                    
                    if (currentForeground != hWnd)
                    {
                        _previousWindow = currentForeground; // Update history: We came FROM here
                    }
                    _lastTargetWindow = hWnd;
                }
            }
            
            // 1. Force the system to allow us to set foreground
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

            // 2. Handle Minimized State
            if (NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                // Sometimes ShowWindow is needed even if not minimized to force state update
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }

            // 3. Robust Activation Logic (Focus Stealing)
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow == hWnd) return;

            uint foregroundThreadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            uint currentThreadId = NativeMethods.GetCurrentThreadId();
            uint targetThreadId = NativeMethods.GetWindowThreadProcessId(hWnd, IntPtr.Zero);

            // We attach input to the thread of the CURRENT foreground window, 
            // so we can "borrow" its right to set the foreground window.
            bool attached = false;
            
            if (foregroundThreadId != currentThreadId)
            {
                attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            // Try to set foreground
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.BringWindowToTop(hWnd);

            // Sometimes we need to pump the input
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
            
            // Final fallback: SwitchToThisWindow (Simulate Alt-Tab)
            NativeMethods.SwitchToThisWindow(hWnd, true);
        }
        public void MinimizeWindow(IntPtr hWnd)
        {
             NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
        }
    }
}
