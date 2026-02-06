using System;

namespace WinManico.Core
{
    public enum LogLevel
    {
        None = 0,
        Error = 1,
        Info = 2,
        Debug = 3
    }

    public static class Logger
    {
        // Cache the current log level to avoid reading settings constantly if needed, 
        // but for now accessing Settings.Load() might be heavy if done every time.
        // Better to have Settings.Instance if possible, but currently Settings are loaded frequently.
        // We will assume the caller passes the level or we check a static property.
        // Actually, Settings.Load() reads from disk. We should probably NOT call it every log.
        // Let's add a static property to Logger that is updated when Settings changes, or just rely on a static Settings instance if one existed.
        
        // Simpler approach given current architecture:
        // We will read the LogLevel from a static property that we assume is set at startup or explicitly updated.
        public static LogLevel CurrentLevel { get; set; } = LogLevel.Info;

        public static void Error(string message)
        {
            if (CurrentLevel >= LogLevel.Error)
            {
                Console.WriteLine($"[ERROR] {message}");
            }
        }

        public static void Info(string message)
        {
            if (CurrentLevel >= LogLevel.Info)
            {
                Console.WriteLine($"[INFO] {message}");
            }
        }

        public static void Debug(string message)
        {
            if (CurrentLevel >= LogLevel.Debug)
            {
                Console.WriteLine($"[DEBUG] {message}");
            }
        }
    }
}
