using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinManico.Core
{
    public class AppConfig
    {
        public string ProcessName { get; set; }
        public string ShortcutKey { get; set; } // "1", "Q", "F1", etc.
        public string? ExecutablePath { get; set; } // Optional: path to launch if not running
    }

    public class Settings
    {
        private static readonly string ConfigPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public List<AppConfig> AppConfigs { get; set; } = new List<AppConfig>();
        public bool AutoStartAsAdmin { get; set; } = false;
        public bool WhitelistMode { get; set; } = false;

        public static Settings Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var loadedSettings = JsonSerializer.Deserialize<Settings>(json);
                    
                    // Sync Logger
                    if (loadedSettings != null)
                    {
                        Logger.CurrentLevel = loadedSettings.LogLevel;
                        return loadedSettings;
                    }
                }
                catch { }
            }

            var settings = new Settings();
            // Seed defaults if config file is missing
            settings.AppConfigs.Add(new AppConfig { 
                ProcessName = "notepad", 
                ShortcutKey = "N" 
            });
            
            // Default Logger
            Logger.CurrentLevel = settings.LogLevel;
            
            settings.Save();
            return settings;
        }

        public LogLevel LogLevel { get; set; } = LogLevel.Info; // Default to Info

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
