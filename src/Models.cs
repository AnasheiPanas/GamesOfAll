using System;
using System.Collections.Generic;

namespace GamesOfAll
{
    public enum Platform
    {
        Steam = 0,
        Epic = 1,
        Riot = 2,
        Local = 3
    }

    public enum GameKind
    {
        Native = 0,
        Emulated = 1
    }

    public class Game
    {
        public string Id;
        public string Title;
        public Platform Platform;
        public GameKind Kind;
        public string LaunchTarget;   // appid de Steam / AppName de Epic / producto de Riot / ruta exe
        public string InstallDir;
        public string Executable;     // ejecutable directo (Epic, locales y emuladores)
        public string LaunchArgs;
        public string IconSource;     // exe del que se extrae el icono
        public string CoverSlug;      // pista para buscar caratula en Epic
        public string CustomPlatform; // etiqueta propia: "PS2", "N64", "Minecraft"...
        public string RomPath;        // solo juegos emulados
        public string EmulatorId;     // referencia al emulador configurado
        public long SteamPlaySeconds; // tiempo sincronizado desde la caché local de Steam
        public List<GameAccountStats> AccountStats = new List<GameAccountStats>();
        public long LastPlayed;       // epoch segundos
        public int PlayCount;

        public string CoverKey
        {
            get
            {
                string k = (Id ?? "game").Replace(":", "_").Replace("\\", "_").Replace("/", "_")
                                              .Replace(" ", "_").Replace("*", "_").Replace("?", "_")
                                              .Replace("\"", "_").Replace("<", "_").Replace(">", "_")
                                              .Replace("|", "_");
                if (k.Length > 90) k = k.Substring(0, 90);
                return k;
            }
        }

        public string PlatformName
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomPlatform)) return CustomPlatform;
                switch (Platform)
                {
                    case Platform.Steam: return "Steam";
                    case Platform.Epic: return "Epic";
                    case Platform.Riot: return "Riot";
                    default: return "Local";
                }
            }
        }

        public bool IsEmulated { get { return Kind == GameKind.Emulated; } }

        // Nombres de proceso que identifican al juego mientras se ejecuta.
        public List<string> ProcessHints()
        {
            List<string> names = new List<string>();
            if (!string.IsNullOrEmpty(Executable))
            {
                try
                {
                    string n = System.IO.Path.GetFileNameWithoutExtension(Executable).ToLowerInvariant();
                    if (n.Length > 0) names.Add(n);
                }
                catch { }
            }
            return names;
        }
    }

    public class GameState
    {
        public bool Favorite;
        public bool Hidden;
        public long LastPlayed;
        public int PlayCount;
        public long PlaySeconds;
        public List<GameAccountStats> AccountStats = new List<GameAccountStats>();
        public string CustomCover;
        public string Description;
        public string DescriptionSource;
        public string DescriptionSourceUrl;
        public long DescriptionFetchedAt;
        public string ManualDescription;
    }

    public class GameAccountStats
    {
        public string AccountId;
        public string AccountName;
        public long PlaySeconds;
        public long BaselinePlaySeconds;
        public long LocalPlaySeconds;
        public string Rank;
        public int Level;
        public string Region;
        public long UpdatedAt;
        public long OnlineUpdatedAt;
    }

    public class Settings
    {
        public bool MinimizeOnLaunch = true;
        public bool ShowHidden = false;
        public bool AutoDiscover = true;
        public int SortMode = 0;   // 0 = recientes, 1 = A-Z, 2 = mas jugados
        public List<string> IgnoredPaths = new List<string>();
        public int WindowWidth = 0;
        public int WindowHeight = 0;
        public int WindowX = int.MinValue;
        public int WindowY = int.MinValue;
        public int Theme = 0; // 0 oscuro, 1 blanco, 2 rojo y negro
        public string RiotApiKeyProtected;
        public string RiotApiRegion;
        public List<string> IgnoredRiotAccounts = new List<string>();
        public Dictionary<string, string> IgnoredRiotAccountNames = new Dictionary<string, string>();
        public List<Game> CustomGames = new List<Game>();
        public Dictionary<string, GameState> States = new Dictionary<string, GameState>();
        public List<EmulatorConfig> Emulators = new List<EmulatorConfig>();
    }

    public class EmulatorConfig
    {
        public string Id;
        public string Name;
        public string ExePath;
        public string RomFolder;
        public string Arguments = "{rom}";
        public string Extensions = ".iso,.bin,.cue,.chd,.rom,.gb,.gba,.gbc,.nes,.sfc,.smc,.n64,.z64,.v64,.nds,.3ds,.ciso,.rvz,.wbfs";
    }

    internal static class Fmt
    {
        public static string Playtime(long seconds)
        {
            if (seconds < 60) return "";
            double hours = seconds / 3600.0;
            if (hours < 1.0) return Math.Max(1, (int)(seconds / 60)) + " min";
            if (hours < 10.0) return hours.ToString("0.0") + " h";
            return ((int)Math.Round(hours)) + " h";
        }

        public static long ToUnix(DateTime utc)
        {
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static string Ago(long unix)
        {
            if (unix <= 0) return "nunca";
            DateTime when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix).ToLocalTime();
            TimeSpan span = DateTime.Now - when;
            if (span.TotalMinutes < 2) return "hace un momento";
            if (span.TotalHours < 1) return "hace " + (int)span.TotalMinutes + " min";
            if (span.TotalDays < 1) return "hace " + (int)span.TotalHours + " h";
            if (span.TotalDays < 2) return "ayer";
            if (span.TotalDays < 7) return "hace " + (int)span.TotalDays + " días";
            return when.ToString("d MMM yyyy");
        }
    }
}
