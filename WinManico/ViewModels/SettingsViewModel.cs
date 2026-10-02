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
        private ObservableCollection<string> _blacklist;

        [ObservableProperty]
        private string _newProcessName;

        [ObservableProperty]
        private string _newShortcutKey;

        [ObservableProperty]
        private string? _newExecutablePath;

        [ObservableProperty]
        private string _newBlacklistProcessName = "";

        [ObservableProperty]
        private ObservableCollection<string> _runningProcesses = new();

        [ObservableProperty]
        private string? _selectedRunningProcess;

        public SettingsViewModel()
        {
            _settings = Settings.Load(); // Reload fresh
            AppConfigs = new ObservableCollection<AppConfig>(_settings.AppConfigs);
            Blacklist = new ObservableCollection<string>(_settings.Blacklist ?? new System.Collections.Generic.List<string>());
            RefreshRunningProcesses();
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

            string processName = AppConfig.NormalizeProcessName(NewProcessName);

            // Check duplicates
            if (AppConfigs.Any(c => c.ProcessName.Equals(processName, System.StringComparison.OrdinalIgnoreCase)))
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
                ProcessName = processName,
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
        public void AddBlacklist()
        {
            string name = NewBlacklistProcessName;
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(SelectedRunningProcess))
            {
                name = SelectedRunningProcess;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                System.Windows.MessageBox.Show("Please enter or select a process name for the blacklist.", "WinManico");
                return;
            }

            name = name.Trim();
            if (name.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4).Trim();
            }

            if (Blacklist.Any(p => p.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show($"'{name}' is already in the blacklist.", "WinManico");
                return;
            }

            Blacklist.Add(name);
            NewBlacklistProcessName = "";
        }

        [RelayCommand]
        public void RemoveBlacklist(string processName)
        {
            if (!string.IsNullOrEmpty(processName))
            {
                Blacklist.Remove(processName);
            }
        }

        [RelayCommand]
        public void BrowseBlacklistExe()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Blacklisted Game / Application Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                NewBlacklistProcessName = fileName;
            }
        }

        [RelayCommand]
        public void RefreshRunningProcesses()
        {
            try
            {
                var wm = new WindowManager();
                var procs = wm.GetOpenWindows()
                    .Select(w => w.ProcessName)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();

                RunningProcesses = new ObservableCollection<string>(procs);
            }
            catch
            {
                // Fallback to empty if error
            }
        }

        [RelayCommand]
        public void Save()
        {
            _settings.AppConfigs = new System.Collections.Generic.List<AppConfig>(AppConfigs);
            _settings.Blacklist = new System.Collections.Generic.List<string>(Blacklist);
            _settings.Save();
            
            System.Windows.MessageBox.Show("Settings saved and applied successfully!", "WinManico", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
