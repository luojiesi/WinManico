using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using System;
using System.Diagnostics;
using System.Security.Principal;
using Application = System.Windows.Application;
using WinManico.Views;

namespace WinManico
{
    public partial class App : Application
    {
        private NotifyIcon _notifyIcon;
        private SettingsWindow _settingsWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if we should auto-elevate
            var settings = WinManico.Core.Settings.Load();
            if (settings.AutoStartAsAdmin && !IsRunningAsAdministrator())
            {
                // Auto-restart as administrator without prompting
                try
                {
                    var info = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator
                    };
                    System.Diagnostics.Process.Start(info);
                    Shutdown();
                    return;
                }
                catch
                {
                    // User cancelled UAC or error occurred, continue without elevation
                }
            }

            _notifyIcon = new NotifyIcon();
            // Use a default system icon or load one. For now, use a system icon.
            _notifyIcon.Icon = new System.Drawing.Icon("icon.ico"); 
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "WinManico";

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, (s, args) => OpenSettings());
            
            // Add "Restart as Administrator" option if not already elevated
            if (!IsRunningAsAdministrator())
            {
                contextMenu.Items.Add("Restart as Administrator", null, (s, args) => RestartAsAdministrator());
            }
            else
            {
                var adminItem = contextMenu.Items.Add("Running as Administrator ✓");
                adminItem.Enabled = false;
            }
            
            contextMenu.Items.Add("Exit", null, (s, args) => Shutdown());
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, args) => OpenSettings();

            // Prevent app from shutting down when the main window (WinManico Overlay) closes.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        private void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        }

        private bool IsRunningAsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void RestartAsAdministrator()
        {
            var result = System.Windows.MessageBox.Show(
                "WinManico will restart with administrator privileges. This allows keyboard shortcuts to work with elevated processes like Task Manager.\n\nRestart now?",
                "Restart as Administrator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                        UseShellExecute = true,
                        Verb = "runas" // Request elevation
                    };

                    Process.Start(processInfo);
                    Shutdown();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to restart as administrator: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
