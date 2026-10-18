using System;
using System.IO;
using System.Text;

namespace StockNotice
{
    public static class AppLogger
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;

        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return _logPath;
            }
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized) return;
                _initialized = true;
                _logPath = ResolveLogPath();
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            EnsureInitialized();
            string path;
            lock (Sync)
            {
                path = _logPath;
            }
            if (string.IsNullOrEmpty(path)) return;

            StringBuilder line = new StringBuilder();
            line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            line.Append(" [");
            line.Append(level);
            line.Append("] ");
            line.Append(message);
            if (exception != null)
            {
                line.AppendLine();
                line.Append(exception);
            }
            line.AppendLine();

            lock (Sync)
            {
                try
                {
                    File.AppendAllText(path, line.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // Logging must never stop monitoring or close the app.
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (Sync)
            {
                if (_initialized) return;
                _initialized = true;
                _logPath = ResolveLogPath();
            }
        }

        private static string ResolveLogPath()
        {
            string fileName = "stock-notice-" + DateTime.Now.ToString("yyyyMMdd") + ".log";
            string preferred = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StockNotice",
                "logs");

            string directory = TryPrepareDirectory(preferred) ? preferred : fallback;
            if (!TryPrepareDirectory(directory)) return string.Empty;
            return Path.Combine(directory, fileName);
        }

        private static bool TryPrepareDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
