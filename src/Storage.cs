using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace GamesOfAll
{
    // Rutas y persistencia (todo portatil, junto al .exe).
    internal static class Storage
    {
        public static string AppDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar); }
        }

        public static string DataDir { get { return Path.Combine(AppDir, "data"); } }
        public static string CoversDir { get { return Path.Combine(AppDir, "covers"); } }
        public static string SettingsFile { get { return Path.Combine(DataDir, "library.json"); } }
        public static string LogFile { get { return Path.Combine(DataDir, "gamesofall.log"); } }

        public static void EnsureFolders()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            if (!Directory.Exists(CoversDir)) Directory.CreateDirectory(CoversDir);
        }

        public static Settings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return new Settings();
                string json = File.ReadAllText(SettingsFile, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return new Settings();
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = 8 * 1024 * 1024;
                Settings s = ser.Deserialize<Settings>(json);
                if (s == null) return new Settings();
                if (s.CustomGames == null) s.CustomGames = new List<Game>();
                if (s.IgnoredPaths == null) s.IgnoredPaths = new List<string>();
                if (s.States == null) s.States = new Dictionary<string, GameState>(StringComparer.OrdinalIgnoreCase);
                if (s.Emulators == null) s.Emulators = new List<EmulatorConfig>();
                return s;
            }
            catch (Exception ex)
            {
                Log("No se pudo leer la configuracion: " + ex.Message);
                return new Settings();
            }
        }

        public static void SaveSettings(Settings s)
        {
            try
            {
                EnsureFolders();
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = 8 * 1024 * 1024;
                string json = ser.Serialize(s);
                string tmp = SettingsFile + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                if (File.Exists(SettingsFile)) File.Delete(SettingsFile);
                File.Move(tmp, SettingsFile);
            }
            catch (Exception ex)
            {
                Log("No se pudo guardar la configuracion: " + ex.Message);
            }
        }

        public static void Log(string message)
        {
            try
            {
                EnsureFolders();
                File.AppendAllText(LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
