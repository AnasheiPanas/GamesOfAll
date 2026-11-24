using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Entrada alternativa solo para previsualizar dialogos durante el desarrollo.
    internal static class PreviewEntry
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            float scale = 1f;
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                scale = g.DpiX / 96f;
            Theme.Init(scale);
            Storage.EnsureFolders();

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "native";

            if (mode == "stats")
            {
                List<StatEntry> entries = new List<StatEntry>();
                string[] covers = Directory.GetFiles(Storage.CoversDir, "*.png");
                string[] titles = { "Hogwarts Legacy", "Counter-Strike 2", "Resident Evil 5 Gold Edition",
                                    "Hades", "League of Legends", "TLauncher" };
                string[] platforms = { "Epic", "Steam", "Local", "Steam", "Riot", "Local" };
                long[] times = { 128 * 3600, 74 * 3600 + 1200, 12 * 3600, 9 * 3600 + 600, 3 * 3600, 2400 };
                for (int i = 0; i < titles.Length; i++)
                {
                    StatEntry e = new StatEntry();
                    e.Title = titles[i];
                    e.Platform = platforms[i];
                    e.PlaySeconds = times[i];
                    e.LastPlayed = Fmt.ToUnix(DateTime.UtcNow.AddDays(-(i + 1)));
                    e.PlayCount = 14 - i * 2;
                    if (covers.Length > 0)
                        e.Cover = Covers.LoadFile(covers[i % covers.Length]);
                    entries.Add(e);
                }
                Application.Run(new StatsDialog(entries, "236 h en total  ·  6 juegos con tiempo"));
                return;
            }

            if (mode == "discovery")
            {
                Application.Run(new DiscoveryDialog(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
                return;
            }

            AddGameDialog dlg = new AddGameDialog(@"C:\Games\Outward\Outward Definitive Edition.exe");

            if (mode == "emulated")
            {
                MethodInfo setKind = typeof(AddGameDialog).GetMethod("SetKind",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (setKind != null) setKind.Invoke(dlg, new object[] { GameKind.Emulated });
            }

            Application.Run(dlg);
        }
    }
}
