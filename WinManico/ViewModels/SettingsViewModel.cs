using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WinManico.Core;

namespace WinManico.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly Settings _settings;

        [ObservableProperty]
        private ObservableCollection<AppConfig> _appConfigs;

        [ObservableProperty]
        private string _newProcessName;

        [ObservableProperty]
        private string _newShortcutKey;

        [ObservableProperty]
        private string? _newExecutablePath;

        public SettingsViewModel()
        {
            _settings = Settings.Load(); // Reload fresh
            AppConfigs = new ObservableCollection<AppConfig>(_settings.AppConfigs);
        }

        public bool AutoStartAsAdmin
        {
            get => _settings.AutoStartAsAdmin;
            set
            {
                if (_settings.AutoStartAsAdmin != value)
                {
                    _settings.AutoStartAsAdmin = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool WhitelistMode
        {
            get => _settings.WhitelistMode;
            set
            {
                if (_settings.WhitelistMode != value)
                {
                    _settings.WhitelistMode = value;
                    OnPropertyChanged();
                }
            }
        }

        [RelayCommand]
        public void AddConfig()
        {
            if (string.IsNullOrWhiteSpace(NewProcessName) || string.IsNullOrWhiteSpace(NewShortcutKey))
            {
                System.Windows.MessageBox.Show("Please enter both Process Name and Key.");
                return;
            }

            string key = NewShortcutKey.ToUpper();
            if (key.Length > 1) {
                 System.Windows.MessageBox.Show("Key must be a single character/number (e.g. 'Q', '1').");
                 return;
            }

            // Check duplicates
            if (AppConfigs.Any(c => c.ProcessName.Equals(NewProcessName, System.StringComparison.OrdinalIgnoreCase)))
            {
                 System.Windows.MessageBox.Show("This app is already configured.");
                 return;
            }
            
            if (AppConfigs.Any(c => c.ShortcutKey.Equals(key, System.StringComparison.OrdinalIgnoreCase)))
            {
                 System.Windows.MessageBox.Show("This key is already used.");
                 return;
            }

            var newConfig = new AppConfig 
            { 
                ProcessName = NewProcessName, 
                ShortcutKey = key,
                ExecutablePath = string.IsNullOrWhiteSpace(NewExecutablePath) ? null : NewExecutablePath
            };
            AppConfigs.Add(newConfig);
            
            // Clear inputs
            NewProcessName = "";
            NewShortcutKey = "";
            NewExecutablePath = "";
        }

        [RelayCommand]
        public void RemoveConfig(AppConfig config)
        {
            if (config != null)
            {
                AppConfigs.Remove(config);
            }
        }

        [RelayCommand]
        public void BrowseExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Application Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                NewExecutablePath = dialog.FileName;
                
                // Auto-derive process name from exe filename (without .exe extension)
                var fileName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(NewProcessName))
                {
                    NewProcessName = fileName;
                }
            }
        }

        [RelayCommand]
        public void Save()
        {
            _settings.AppConfigs = new System.Collections.Generic.List<AppConfig>(AppConfigs);
            _settings.Save();
            
            System.Windows.MessageBox.Show("Settings Saved. Please restart the application for key changes to take full effect.", "WinManico");
            // Could trigger a reload event, but restart is safer for hooks.
        }
    }
}
