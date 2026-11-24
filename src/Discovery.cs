using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace GamesOfAll
{
    internal class GameCandidate
    {
        public string Title;
        public string Exe;
        public string Args;
        public string WorkingDir;
        public string Folder;
        public string Source;
        public bool AutoAdd;   // se añade solo al abrir el launcher

        public string Key { get { return (Exe ?? "").ToLowerInvariant(); } }
    }

    // Busca juegos instalados fuera de las tiendas: accesos directos del
    // escritorio/menu inicio y carpetas tipicas de juegos.
    internal static class Discovery
    {
        private static readonly string[] JunkParts =
        {
            "unins", "uninstall", "setup", "installer", "install", "update", "updater", "patcher",
            "redist", "vcredist", "dxsetup", "dxwebsetup", "directx", "dotnet",
            "crash", "reporter", "bugreport", "readme", "help", "manual", "docs",
            "winword", "excel", "powerpnt", "outlook", "onenote", "mspaint", "notepad", "wordpad",
            "chrome", "firefox", "msedge", "brave", "opera", "vivaldi", "iexplore", "torrent",
            "discord", "slack", "telegram", "whatsapp", "zoom", "teams",
            "obs", "jdownloader", "qbittorrent", "bitcomet", "utorrent",
            "logitech", "lghub", "ghub", "razer", "corsair", "iriun", "webcam",
            "visual studio", "vscode", "vscodium", "opencode", "git", "python", "node",
            "calculator", "calc", "powershell", "cmd", "terminal", "explorer", "control",
            "steam", "epicgameslauncher", "riotclient", "riot client", "battle.net", "battlenet",
            "rockstar games launcher", "ubisoft", "upc", "ea app", "gog galaxy",
            "easyanticheat", "eac_", "battleye", "beservice", "unitycrashhandler",
            "xbox", "gamebar", "phone", "camera"
        };

        private static readonly string[] KnownLaunchers =
        {
            "tlauncher", "battly", "prism", "multimc", "atlauncher",
            "technic", "modrinth", "gdlauncher", "hmcl", "sklauncher"
        };

        private static readonly string[] JunkDirs =
        {
            "redist", "_commonredist", "commonredist", "directx", "support", "docs", "doc",
            "tools", "installer", "install", "uninstall", ".git", "__pycache__", "cache"
        };

        public static List<GameCandidate> DiscoverAll()
        {
            Dictionary<string, GameCandidate> found = new Dictionary<string, GameCandidate>(StringComparer.OrdinalIgnoreCase);
            foreach (GameCandidate c in FromShortcuts()) Add(found, c);
            foreach (GameCandidate c in FromGameFolders()) Add(found, c);

            List<GameCandidate> list = new List<GameCandidate>(found.Values);
            list.Sort(delegate(GameCandidate a, GameCandidate b)
            {
                if (a.AutoAdd != b.AutoAdd) return a.AutoAdd ? -1 : 1;
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        private static void Add(Dictionary<string, GameCandidate> map, GameCandidate c)
        {
            if (c == null || string.IsNullOrEmpty(c.Exe)) return;
            if (!map.ContainsKey(c.Key)) map[c.Key] = c;
        }

        // ------------------------------------------------------- ACCESOS DIRECTOS

        public static List<GameCandidate> FromShortcuts()
        {
            List<GameCandidate> list = new List<GameCandidate>();
            List<string> roots = new List<string>();
            SafeAddRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            SafeAddRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            SafeAddRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
            SafeAddRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                string[] files;
                try { files = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (string lnk in files)
                {
                    try
                    {
                        string title = Path.GetFileNameWithoutExtension(lnk);
                        if (string.IsNullOrEmpty(title) || IsJunk(title)) continue;
                        if (!seen.Add(title.ToLowerInvariant())) continue;

                        string args, workDir, icon;
                        string target = ResolveShortcut(lnk, out args, out workDir, out icon);
                        if (string.IsNullOrEmpty(target)) continue;
                        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!File.Exists(target)) continue;
                        if (IsJunkPath(target)) continue;

                        string folder = Path.GetDirectoryName(target);

                        GameCandidate c = new GameCandidate();
                        c.Title = CleanTitle(title);
                        c.Exe = target;
                        c.Args = args;
                        c.WorkingDir = workDir;
                        c.Folder = folder;
                        c.Source = "Acceso directo";
                        c.AutoAdd = IsKnownLauncher(title) || IsUnderGameFolder(target) || HasGameMarkers(folder);
                        list.Add(c);
                    }
                    catch { }
                }
            }
            return list;
        }

        private static void SafeAddRoots(List<string> roots, string root)
        {
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root) &&
                !roots.Contains(root, StringComparer.OrdinalIgnoreCase))
                roots.Add(root);
        }

        private static string ResolveShortcut(string lnk, out string args, out string workDir, out string icon)
        {
            args = null; workDir = null; icon = null;
            object shell = null;
            object link = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                shell = Activator.CreateInstance(shellType);
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                    new object[] { lnk });
                Type linkType = link.GetType();
                string target = (string)linkType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null);
                args = (string)linkType.InvokeMember("Arguments", BindingFlags.GetProperty, null, link, null);
                workDir = (string)linkType.InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, link, null);
                icon = (string)linkType.InvokeMember("IconLocation", BindingFlags.GetProperty, null, link, null);
                return target;
            }
            catch { return null; }
            finally
            {
                if (link != null) { try { Marshal.ReleaseComObject(link); } catch { } }
                if (shell != null) { try { Marshal.ReleaseComObject(shell); } catch { } }
            }
        }

        // ------------------------------------------------------------ CARPETAS

        public static IEnumerable<string> GameRoots()
        {
            List<string> roots = new List<string>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    string r = drive.RootDirectory.FullName;
                    roots.Add(Path.Combine(r, "Games"));
                    roots.Add(Path.Combine(r, "Juegos"));
                    roots.Add(Path.Combine(r, "Game"));
                }
            }
            catch { }
            return roots;
        }

        public static List<GameCandidate> FromGameFolders()
        {
            List<GameCandidate> list = new List<GameCandidate>();
            foreach (string root in GameRoots())
            {
                if (!Directory.Exists(root)) continue;
                string[] dirs;
                try { dirs = Directory.GetDirectories(root); }
                catch { continue; }

                foreach (string dir in dirs)
                {
                    try
                    {
                        GameCandidate c = ScanGameFolder(dir);
                        if (c != null) list.Add(c);
                    }
                    catch { }
                }
            }
            return list;
        }

        // Elige el ejecutable mas probable dentro de una carpeta de juego.
        public static GameCandidate ScanGameFolder(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            List<string> exes = new List<string>();
            CollectExes(folder, 0, 3, exes);
            if (exes.Count == 0) return null;

            string folderName = Path.GetFileName(folder);
            string folderNorm = Norm(folderName);
            string best = null;
            long bestScore = long.MinValue;

            foreach (string exe in exes)
            {
                string name = Path.GetFileNameWithoutExtension(exe);
                if (IsJunk(name)) continue;
                long size = 0;
                try { size = new FileInfo(exe).Length; } catch { }
                if (size < 120 * 1024) continue;

                long score = Math.Min(size / 1024, 400000);
                string exeNorm = Norm(name);
                if (exeNorm.Length > 2 && (folderNorm.Contains(exeNorm) || exeNorm.Contains(folderNorm)))
                    score += 900000;

                int depth = CountDepth(exe) - CountDepth(folder);
                score -= depth * 30000;

                if (score > bestScore) { bestScore = score; best = exe; }
            }
            if (best == null) return null;

            GameCandidate c = new GameCandidate();
            c.Title = CleanTitle(folderName);
            c.Exe = best;
            c.Folder = folder;
            c.WorkingDir = folder;
            c.Source = "Carpeta";
            c.AutoAdd = true;
            return c;
        }

        private static int CountDepth(string path)
        {
            int n = 0;
            foreach (char ch in path) if (ch == '\\' || ch == '/') n++;
            return n;
        }

        private static void CollectExes(string dir, int depth, int maxDepth, List<string> exes)
        {
            if (depth > maxDepth) return;
            try
            {
                foreach (string file in Directory.GetFiles(dir, "*.exe")) exes.Add(file);
            }
            catch { }

            if (depth >= maxDepth) return;
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { return; }

            foreach (string sub in subdirs)
            {
                string name = Path.GetFileName(sub).ToLowerInvariant();
                bool skip = false;
                foreach (string bad in JunkDirs) if (name == bad || name.Contains(bad)) { skip = true; break; }
                if (skip) continue;
                CollectExes(sub, depth + 1, maxDepth, exes);
            }
        }

        // ----------------------------------------------------------- HEURISTICA

        private static bool IsKnownLauncher(string title)
        {
            string t = title.ToLowerInvariant();
            foreach (string k in KnownLaunchers) if (t.Contains(k)) return true;
            return false;
        }

        private static bool IsJunk(string text)
        {
            string t = text.ToLowerInvariant();
            foreach (string bad in JunkParts) if (t.Contains(bad)) return true;
            return false;
        }

        private static bool IsJunkPath(string path)
        {
            string p = path.ToLowerInvariant();
            if (p.Contains("\\windows\\") || p.Contains("\\microsoft office\\") ||
                p.Contains("\\windowsapps\\") || p.Contains("\\common files\\") ||
                p.Contains("\\microsoft\\edge") || p.Contains("\\temp\\") ||
                p.Contains("\\appdata\\local\\programs\\")) return true;
            return IsJunk(Path.GetFileNameWithoutExtension(path));
        }

        public static bool IsUnderGameFolder(string path)
        {
            try
            {
                string full = Path.GetFullPath(path).ToLowerInvariant();
                foreach (string root in GameRoots())
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    if (full.StartsWith(Path.GetFullPath(root).ToLowerInvariant() + "\\")) return true;
                }
            }
            catch { }
            return false;
        }

        // Marcadores tipicos de un juego instalado (Unity, Unreal, Steam API...).
        public static bool HasGameMarkers(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            try
            {
                // Las apps Electron (CurseForge, Discord, Opera...) tambien llevan .pak.
                if (File.Exists(Path.Combine(dir, "icudtl.dat")) &&
                    File.Exists(Path.Combine(dir, "resources.pak"))) return false;

                foreach (string file in Directory.GetFiles(dir))
                {
                    string n = Path.GetFileName(file).ToLowerInvariant();
                    if (n == "unityplayer.dll" || n == "gameassembly.dll" || n == "mono.dll" ||
                        n == "steam_api.dll" || n == "steam_api64.dll" || n == "steamclient_loader.exe") return true;
                    if (n.EndsWith(".pak") || n.EndsWith(".assets") || n.EndsWith(".uasset") || n.EndsWith(".bundle"))
                        return true;
                }
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string n = Path.GetFileName(sub).ToLowerInvariant();
                    if (n == "binaries" || n == "content" || n == "engine") return true;
                }
            }
            catch { }
            return false;
        }

        // Nombres de proceso (sin extension) de los .exe de una carpeta de juego.
        public static List<string> ProcessNamesInFolder(string folder)
        {
            List<string> names = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return names;

            List<string> exes = new List<string>();
            CollectExes(folder, 0, 2, exes);
            foreach (string exe in exes)
            {
                try
                {
                    string n = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
                    if (n.Length == 0 || IsJunk(n)) continue;
                    if (!names.Contains(n)) names.Add(n);
                }
                catch { }
            }
            return names;
        }

        public static string CleanTitle(string raw)        {
            if (string.IsNullOrEmpty(raw)) return "Juego";
            string t = raw.Replace('_', ' ').Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t;
        }

        private static string Norm(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }
    }
}
