using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace WinManico.Core
{
    public class KeyboardHook : IDisposable
    {
        private NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public event EventHandler DoubleAltDetected;
        public event EventHandler<bool> AltKeyChanged; // true = down, false = up
        
        // Pass the string representation of the key (e.g. "1", "Q")
        public delegate void KeyPressedEventHandler(object sender, string key, ref bool handled);
        public event KeyPressedEventHandler KeyPressed;
        public event EventHandler AltSessionCancelled;

        private bool _isAltDown = false;
        private bool _currentAltSessionHadKeypress = false; // New flag to track usage
        private DateTime _lastAltUpTime = DateTime.MinValue;
        private const int DoublePressThresholdMs = 300; // Tighter threshold (300ms) for cleaner feel
        private readonly Settings _settings;

        public KeyboardHook(Settings settings)
        {
            _settings = settings;
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc,
                    NativeMethods.GetModuleHandle(null), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                // Handle Alt (Left Alt)
                if (vkCode == NativeMethods.VK_LMENU) 
                {
                    if (wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN || wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
                    {
                        if (!_isAltDown) 
                        {
                            _isAltDown = true;
                            
                            // Check for modifiers immediately. If Ctrl/Shift/Win are down, this is NOT a pure Alt tap.
                            bool isCtrlDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                            bool isShiftDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                            bool isWinDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 || 
                                             (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

                            if (isCtrlDown || isShiftDown || isWinDown)
                            {
                                Logger.Debug("[HOOK-DEBUG] Alt Down with Modifiers. Tainting session.");
                                _currentAltSessionHadKeypress = true; // Mark as "used" so it doesn't count as a tap
                            }
                            else
                            {
                                _currentAltSessionHadKeypress = false; // Reset for new pure session
                            }

                            Logger.Debug($"[HOOK-DEBUG] Alt DOWN. Ticks={DateTime.Now.Ticks}");
                            
                            // Check for Double Click (Down -> Up -> Down)
                            var diff = (DateTime.Now - _lastAltUpTime).TotalMilliseconds;
                            Logger.Debug($"[HOOK-DEBUG] Time since last UP: {diff}ms (Threshold: {DoublePressThresholdMs})");
                            
                            if (diff < DoublePressThresholdMs)
                            {
                                Logger.Debug("[HOOK-DEBUG] *** DOUBLE ALT DETECTED ***");
                                DoubleAltDetected?.Invoke(this, EventArgs.Empty);
                            }
                            
                            AltKeyChanged?.Invoke(this, true);
                        }
                    }
                    else if (wParam == (IntPtr)NativeMethods.WM_SYSKEYUP || wParam == (IntPtr)NativeMethods.WM_KEYUP)
                    {
                         _isAltDown = false;
                         
                         if (_currentAltSessionHadKeypress)
                         {
                             // If we used the Alt key for something (e.g. Alt+Tab), it doesn't count as a "tap"
                             _lastAltUpTime = DateTime.MinValue;
                             Logger.Debug("[HOOK-DEBUG] Alt UP. Session was USED. Reset timer.");
                         }
                         else
                         {
                             // Clean tap
                             _lastAltUpTime = DateTime.Now; 
                             Logger.Debug($"[HOOK-DEBUG] Alt UP. Clean tap. Ticks={_lastAltUpTime.Ticks}");
                         }

                         AltKeyChanged?.Invoke(this, false);
                    }
                }
                // Only process non-Alt keys if flag says Alt is down
                else if (_isAltDown)
                {
                    // Mark this session as having activity
                    if ((wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
                    {
                        _currentAltSessionHadKeypress = true;
                        
                        // If the pressed key is a modifier, CANCEL the session (Hide UI)
                        if (IsModifier(vkCode))
                        {
                            Logger.Debug($"[HOOK] Modifier {vkCode} pressed while Alt down -> Cancel Session");
                            AltSessionCancelled?.Invoke(this, EventArgs.Empty);
                        }
                    }

                    // Double-check physical Alt state to catch stolen key-up events
                    short altState = NativeMethods.GetAsyncKeyState(NativeMethods.VK_LMENU);
                    bool isAltPhysicallyDown = (altState & 0x8000) != 0;
                    
                    if (!isAltPhysicallyDown)
                    {
                        // Alt flag is stuck! Reset it
                        Logger.Debug("[HOOK] *** FIXING STUCK ALT FLAG - Alt physically released but flag was true ***");
                        _isAltDown = false;
                        _lastAltUpTime = DateTime.MinValue; // Invalidate
                        AltKeyChanged?.Invoke(this, false);
                    }
                    else
                    {
                        // Alt is actually down, process the key
                        string keyString = GetKeyString(vkCode);
                        if (keyString != null)
                        {
                            if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN)
                            {
                                // Check if this key is "Interesting" to us
                                bool isInteresting = IsKeyConfigured(keyString);

                                // STRICT MODIFIER CHECK:
                                // If Ctrl, Shift, or Win are also down, this is NOT a pure Alt shortcut (e.g. Ctrl+Alt+Q)
                                // We should ignore it.
                                bool isCtrlDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
                                bool isShiftDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                                bool isWinDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 || 
                                                 (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

                                if (isInteresting && !isCtrlDown && !isShiftDown && !isWinDown)
                                {
                                    Logger.Debug($"[HOOK] Key '{keyString}' pressed while Alt down");
                                    bool handled = false; // Default to FALSE (Pass through) unless listener claims it
                                    KeyPressed?.Invoke(this, keyString, ref handled);
                                    if (handled)
                                    {
                                        Logger.Debug($"[HOOK] Key '{keyString}' consumed");
                                        // If we consumed it, we technically used it. 
                                        // But we already set _currentAltSessionHadKeypress = true above.
                                        return (IntPtr)1; // Consume the key
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private string GetKeyString(int vkCode)
        {
            if (vkCode >= 0x30 && vkCode <= 0x39) // 0-9
                return ((char)('0' + (vkCode - 0x30))).ToString();
            
            if (vkCode >= 0x41 && vkCode <= 0x5A) // A-Z
                return ((char)('A' + (vkCode - 0x41))).ToString();

            return null;
        }

        private bool IsKeyConfigured(string key)
        {
            // Numbers are always supported
            if (char.IsDigit(key[0])) return true;

            // Check settings
            return _settings.AppConfigs.Any(c => c.ShortcutKey.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsModifier(int vkCode)
        {
            return vkCode == NativeMethods.VK_CONTROL || 
                   vkCode == NativeMethods.VK_SHIFT || 
                   vkCode == NativeMethods.VK_LWIN || 
                   vkCode == NativeMethods.VK_RWIN ||
                   vkCode == 0xA0 || // VK_LSHIFT
                   vkCode == 0xA1 || // VK_RSHIFT
                   vkCode == 0xA2 || // VK_LCONTROL
                   vkCode == 0xA3;   // VK_RCONTROL
        }

        public void Dispose()
        {
            NativeMethods.UnhookWindowsHookEx(_hookID);
        }
    }
}
