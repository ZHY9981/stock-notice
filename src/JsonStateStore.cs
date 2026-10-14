using System;
using System.IO;
using System.Web.Script.Serialization;

namespace StockNotice
{
    public class JsonStateStore
    {
        private readonly JavaScriptSerializer _serializer;
        private readonly string _statePath;

        public JsonStateStore()
        {
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = int.MaxValue;
            _statePath = ResolveStatePath();
        }

        public string StatePath
        {
            get { return _statePath; }
        }

        private static string ResolveStatePath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string preferred = Path.Combine(baseDirectory, "data");

            try
            {
                Directory.CreateDirectory(preferred);
                string probe = Path.Combine(preferred, ".write-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return Path.Combine(preferred, "state.json");
            }
            catch
            {
                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StockNotice",
                    "data");
                Directory.CreateDirectory(fallback);
                return Path.Combine(fallback, "state.json");
            }
        }

        public AppState Load()
        {
            if (!File.Exists(_statePath))
            {
                return new AppState();
            }

            try
            {
                string json = File.ReadAllText(_statePath);
                AppState state = _serializer.Deserialize<AppState>(json);
                if (state == null) state = new AppState();
                state.Normalize();
                return state;
            }
            catch (Exception ex)
            {
                TryBackupCorruptFile(ex);
                return new AppState();
            }
        }

        public void Save(AppState state)
        {
            if (state == null) throw new ArgumentNullException("state");

            state.Normalize();
            state.UpdatedAt = DateTime.Now;

            string directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string json = _serializer.Serialize(state);
            string temp = _statePath + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(_statePath))
            {
                string backup = _statePath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Replace(temp, _statePath, backup);
            }
            else
            {
                File.Move(temp, _statePath);
            }
        }

        private void TryBackupCorruptFile(Exception ex)
        {
            try
            {
                if (!File.Exists(_statePath)) return;
                string backup = _statePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(_statePath, backup, true);
                File.WriteAllText(_statePath + ".error.txt", ex.ToString());
            }
            catch
            {
                // Recovery must not prevent the application from starting.
            }
        }
    }
}
