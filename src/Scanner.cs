using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace GamesOfAll
{
    // Detecta los juegos instalados en Steam, Epic Games y Riot Client.
    internal static class Scanner
    {
        public static List<Game> ScanAll()
        {
            List<Game> games = new List<Game>();
            SafeAdd(games, ScanSteam, "Steam");
            SafeAdd(games, ScanEpic, "Epic");
            SafeAdd(games, ScanRiot, "Riot");

            // Sin duplicados por Id
            Dictionary<string, Game> unique = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
            foreach (Game g in games)
            {
                if (!unique.ContainsKey(g.Id)) unique[g.Id] = g;
            }
            return new List<Game>(unique.Values);
        }

        private static void SafeAdd(List<Game> all, Func<List<Game>> scan, string name)
        {
            try { all.AddRange(scan()); }
            catch (Exception ex) { Storage.Log("Error al detectar " + name + ": " + ex.Message); }
        }

        // ---------------------------------------------------------------- STEAM

        private static readonly HashSet<string> SteamBlocklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "228980", // Steamworks Common Redistributables
            "250820", // SteamVR
            "1070560","1391110","1628350","1887720","2180100","1493710", // runtimes / proton
            "96100","1054830","1245620"
        };

        public static List<Game> ScanSteam()
        {
            List<Game> games = new List<Game>();
            string steamPath = FindSteamPath();
            if (steamPath == null) { Storage.Log("Steam no encontrado."); return games; }

            List<string> libraryRoots = new List<string>();
            libraryRoots.Add(steamPath);

            string libFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libFile))
                libFile = Path.Combine(steamPath, "config", "libraryfolders.vdf");

            if (File.Exists(libFile))
            {
                try
                {
                    Dictionary<string, object> root = Vdf.Parse(File.ReadAllText(libFile));
                    Dictionary<string, object> folders = Vdf.GetMap(root, "libraryfolders");
                    if (folders == null) folders = root;

                    foreach (KeyValuePair<string, object> kv in folders)
                    {
                        Dictionary<string, object> entry = kv.Value as Dictionary<string, object>;
                        if (entry != null)
                        {
                            string p = Vdf.GetString(entry, "path");
                            if (!string.IsNullOrEmpty(p) && !libraryRoots.Contains(p, StringComparer.OrdinalIgnoreCase))
                                libraryRoots.Add(p);
                        }
                        else
                        {
                            string p = kv.Value as string;
                            if (!string.IsNullOrEmpty(p) && p.Contains(":\\") && !libraryRoots.Contains(p, StringComparer.OrdinalIgnoreCase))
                                libraryRoots.Add(p);
                        }
                    }
                }
                catch (Exception ex) { Storage.Log("libraryfolders.vdf: " + ex.Message); }
            }

            foreach (string root in libraryRoots)
            {
                string steamapps = Path.Combine(root, "steamapps");
                if (!Directory.Exists(steamapps)) continue;

                string[] manifests;
                try { manifests = Directory.GetFiles(steamapps, "appmanifest_*.acf"); }
                catch { continue; }

                foreach (string manifest in manifests)
                {
                    try
                    {
                        Game g = ReadSteamManifest(manifest, steamapps);
                        if (g != null) games.Add(g);
                    }
                    catch (Exception ex) { Storage.Log("appmanifest " + manifest + ": " + ex.Message); }
                }
            }
            return games;
        }

        private static Game ReadSteamManifest(string manifestPath, string steamappsDir)
        {
            Dictionary<string, object> root = Vdf.Parse(File.ReadAllText(manifestPath));
            Dictionary<string, object> state = Vdf.GetMap(root, "AppState");
            if (state == null) return null;

            string appId = Vdf.GetString(state, "appid");
            if (string.IsNullOrEmpty(appId))
            {
                Match m = Regex.Match(Path.GetFileName(manifestPath), @"appmanifest_(\d+)\.acf");
                if (m.Success) appId = m.Groups[1].Value;
            }
            if (string.IsNullOrEmpty(appId)) return null;

            string name = Vdf.GetString(state, "name");
            string installDir = Vdf.GetString(state, "installdir");
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(installDir)) return null;

            if (SteamBlocklist.Contains(appId)) return null;
            if (Regex.IsMatch(name, @"steamworks|proton|steam linux runtime|steamvr|redistributabl|steam audio|steam link",
                    RegexOptions.IgnoreCase)) return null;

            string fullDir = Path.Combine(steamappsDir, "common", installDir);
            if (!Directory.Exists(fullDir)) return null;

            Game g = new Game();
            g.Id = "steam:" + appId;
            g.Title = name.Trim();
            g.Platform = Platform.Steam;
            g.LaunchTarget = appId;
            g.InstallDir = fullDir;
            g.LastPlayed = Vdf.GetLong(state, "LastPlayed", 0);
            return g;
        }

        private static string FindSteamPath()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string p = k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(p))
                        {
                            p = p.Replace('/', '\\');
                            if (Directory.Exists(p)) return p;
                        }
                    }
                }
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string p = k.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
                    }
                }
            }
            catch { }
            string fallback = @"C:\Program Files (x86)\Steam";
            return Directory.Exists(fallback) ? fallback : null;
        }

        // ----------------------------------------------------------------- EPIC

        public static List<Game> ScanEpic()
        {
            List<Game> games = new List<Game>();
            List<string> manifestDirs = new List<string>();
            manifestDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"));
            manifestDirs.Add(@"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests");

            foreach (string dir in manifestDirs)
            {
                if (!Directory.Exists(dir)) continue;
                string[] files;
                try { files = Directory.GetFiles(dir, "*.item"); }
                catch { continue; }

                foreach (string file in files)
                {
                    try
                    {
                        Game g = ReadEpicManifest(file);
                        if (g != null) games.Add(g);
                    }
                    catch (Exception ex) { Storage.Log("manifiesto Epic " + file + ": " + ex.Message); }
                }
                if (games.Count > 0) break;
            }
            return games;
        }

        private static Game ReadEpicManifest(string path)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = 8 * 1024 * 1024;
            Dictionary<string, object> m = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (m == null) return null;

            string display = GetStr(m, "DisplayName");
            string install = GetStr(m, "InstallLocation");
            string exe = GetStr(m, "LaunchExecutable");
            string appName = GetStr(m, "AppName");
            string folderHint = GetStr(m, "MandatoryAppFolderName");

            if (string.IsNullOrEmpty(display) || string.IsNullOrEmpty(install) || string.IsNullOrEmpty(appName))
                return null;
            if (!Directory.Exists(install)) return null;
            if (Regex.IsMatch(display, @"^(DirectX|GameInput|Unreal Engine|Epic Online|Epic Games Launcher|UnrealDatasmith)",
                    RegexOptions.IgnoreCase)) return null;

            object appFlag;
            if (m.TryGetValue("bIsApplication", out appFlag) && appFlag is bool && !(bool)appFlag) return null;

            string exePath = null;
            if (!string.IsNullOrEmpty(exe))
            {
                string candidate = Path.Combine(install, exe.Replace('/', '\\'));
                if (File.Exists(candidate)) exePath = candidate;
            }

            Game g = new Game();
            g.Id = "epic:" + appName;
            g.Title = display.Trim();
            g.Platform = Platform.Epic;
            g.LaunchTarget = appName;
            g.InstallDir = install;
            g.Executable = exePath;
            g.LaunchArgs = GetStr(m, "LaunchCommand");
            g.IconSource = exePath;
            g.CoverSlug = Slug(display);
            if (!string.IsNullOrEmpty(folderHint)) g.CoverSlug = g.CoverSlug + "|" + Slug(folderHint);
            return g;
        }

        private static string GetStr(Dictionary<string, object> map, string key)
        {
            object v;
            if (!map.TryGetValue(key, out v) || v == null) return null;
            return v.ToString();
        }

        public static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string norm = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            bool lastDash = true;
            foreach (char ch in norm)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    lastDash = false;
                }
                else if (!lastDash)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
            return sb.ToString().Trim('-');
        }

        // ----------------------------------------------------------------- RIOT

        public static List<Game> ScanRiot()
        {
            List<Game> games = new List<Game>();
            List<string> roots = new List<string>
            {
                @"C:\Riot Games",
                @"C:\Program Files\Riot Games",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games")
            };
            string mainRoot = null;
            foreach (string r in roots) { if (Directory.Exists(Path.Combine(r, "Riot Client"))) { mainRoot = r; break; } }
            if (mainRoot == null)
            {
                foreach (string r in roots) { if (Directory.Exists(r)) { mainRoot = r; break; } }
            }
            if (mainRoot == null) { Storage.Log("Riot Client no encontrado."); return games; }

            string riotClient = Path.Combine(mainRoot, "Riot Client", "RiotClientServices.exe");
            if (!File.Exists(riotClient)) riotClient = null;

            AddRiot(games, mainRoot, "League of Legends", "league_of_legends", "LEAGUE OF LEGENDS",
                Path.Combine("League of Legends", "LeagueClient.exe"), riotClient);
            AddRiot(games, mainRoot, "Teamfight Tactics", "bacon", "TEAMFIGHT TACTICS",
                Path.Combine("Teamfight Tactics", "Live", "TFTClient.exe"), riotClient);
            AddRiot(games, mainRoot, "VALORANT", "valorant", "VALORANT",
                Path.Combine("VALORANT", "live", "ShooterGame", "Binaries", "Win64", "VALORANT.exe"), riotClient);
            AddRiot(games, mainRoot, "Legends of Runeterra", "lion", "LEGENDS OF RUNETERRA",
                Path.Combine("Legends of Runeterra", "Live", "LoR.exe"), riotClient);
            return games;
        }

        private static void AddRiot(List<Game> games, string root, string folder, string product, string title,
            string exeRelative, string riotClient)
        {
            string dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) return;

            string exe = Path.Combine(root, exeRelative);
            if (!File.Exists(exe)) exe = null;

            // League y TFT comparten instalacion: TFT solo existe si tiene su cliente propio.
            if (folder == "Teamfight Tactics" && exe == null) return;

            Game g = new Game();
            g.Id = "riot:" + product;
            g.Title = title;
            g.Platform = Platform.Riot;
            g.LaunchTarget = product;
            g.InstallDir = dir;
            g.Executable = exe;
            g.IconSource = exe;
            g.CoverSlug = null;
            if (riotClient != null) g.LaunchArgs = riotClient;
            games.Add(g);
        }

        public static string FindRiotClient()
        {
            string[] candidates =
            {
                @"C:\Riot Games\Riot Client\RiotClientServices.exe",
                @"C:\Program Files\Riot Games\Riot Client\RiotClientServices.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Riot Games\Riot Client\RiotClientServices.exe")
            };
            foreach (string c in candidates) if (File.Exists(c)) return c;
            return null;
        }
    }
}
