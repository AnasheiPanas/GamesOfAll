using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Descarga y cachea las caratulas. Si no hay caratula en linea, genera una
    // imagen con el icono del propio juego.
    internal static class Covers
    {
        private const int CoverW = 600;
        private const int CoverH = 900;

        public static string RealPath(Game g) { return Path.Combine(Storage.CoversDir, g.CoverKey + ".img"); }
        public static string PlaceholderPath(Game g) { return Path.Combine(Storage.CoversDir, g.CoverKey + ".ph.png"); }

        public static Image Load(Game g, GameState st)
        {
            if (st != null && !string.IsNullOrEmpty(st.CustomCover))
            {
                Image custom = LoadFile(Path.Combine(Storage.CoversDir, st.CustomCover));
                if (custom != null) return custom;
            }
            Image real = LoadFile(RealPath(g));
            if (real != null) return real;
            return LoadFile(PlaceholderPath(g));
        }

        public static Image LoadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 200) return null;
                using (MemoryStream ms = new MemoryStream(data))
                using (Image tmp = Image.FromStream(ms))
                {
                    return new Bitmap(tmp);
                }
            }
            catch { return null; }
        }

        // Devuelve true si hay una imagen nueva que deba recargarse.
        public static bool Ensure(Game g, GameState st)
        {
            try
            {
                if (st != null && !string.IsNullOrEmpty(st.CustomCover) &&
                    File.Exists(Path.Combine(Storage.CoversDir, st.CustomCover)))
                    return false;

                string real = RealPath(g);
                if (File.Exists(real) && new FileInfo(real).Length > 4000) return false;

                bool downloaded = false;
                if (g.Platform == Platform.Steam) downloaded = FetchSteam(g);
                else if (g.Platform == Platform.Epic) downloaded = FetchEpic(g);
                if (downloaded) return true;

                if (!File.Exists(PlaceholderPath(g)))
                {
                    MakePlaceholder(g);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Storage.Log("Caratula de " + g.Title + ": " + ex.Message);
            }
            return false;
        }

        private static bool FetchSteam(Game g)
        {
            string appId = g.LaunchTarget;
            if (string.IsNullOrEmpty(appId)) return false;
            string[] urls =
            {
                "https://cdn.cloudflare.steamstatic.com/steam/apps/" + appId + "/library_600x900.jpg",
                "https://cdn.cloudflare.steamstatic.com/steam/apps/" + appId + "/library_600x900_2x.jpg",
                "https://cdn.cloudflare.steamstatic.com/steam/apps/" + appId + "/header.jpg"
            };
            foreach (string url in urls)
            {
                if (DownloadImage(url, RealPath(g))) return true;
            }
            return false;
        }

        private static bool FetchEpic(Game g)
        {
            if (string.IsNullOrEmpty(g.CoverSlug)) return false;
            string[] slugs = g.CoverSlug.Split('|');
            foreach (string slug in slugs)
            {
                if (string.IsNullOrEmpty(slug)) continue;
                string api = "https://store-content.ak.epicgames.com/api/en-US/content/products/" + slug;
                string json = DownloadString(api);
                if (string.IsNullOrEmpty(json)) continue;

                json = json.Replace("\\/", "/");
                MatchCollection matches = Regex.Matches(json,
                    @"https://cdn[0-9]*\.unrealengine\.com/[^""'\\\s]+?\.(?:jpg|jpeg|png)");
                string best = null;
                string[] preferences = { "1200x1600", "2000x2666", "2560x1440", "1920x1080" };
                foreach (string pref in preferences)
                {
                    foreach (Match m in matches)
                    {
                        if (m.Value.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0) { best = m.Value; break; }
                    }
                    if (best != null) break;
                }
                if (best == null && matches.Count > 0) best = matches[0].Value;
                if (best != null && DownloadImage(best, RealPath(g))) return true;
            }
            return false;
        }

        // ------------------------------------------------------------- DESCARGA

        private static bool DownloadImage(string url, string destination)
        {
            string tmp = destination + ".tmp";
            try
            {
                HttpWebRequest req = CreateRequest(url);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode != HttpStatusCode.OK) return false;
                    string type = resp.ContentType ?? "";
                    if (type.Length > 0 && type.IndexOf("image", StringComparison.OrdinalIgnoreCase) < 0) return false;
                    using (Stream input = resp.GetResponseStream())
                    using (FileStream output = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                    {
                        CopyStream(input, output);
                    }
                }

                using (FileStream fs = File.OpenRead(tmp))
                using (Image img = Image.FromStream(fs))
                {
                    if (img.Width < 100 || img.Height < 100) { TryDelete(tmp); return false; }
                }

                if (File.Exists(destination)) File.Delete(destination);
                File.Move(tmp, destination);
                return true;
            }
            catch (Exception ex)
            {
                Storage.Log("Descarga fallida " + url + " -> " + ex.Message);
                TryDelete(tmp);
                return false;
            }
        }

        private static string DownloadString(string url)
        {
            try
            {
                HttpWebRequest req = CreateRequest(url);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    return reader.ReadToEnd();
            }
            catch { return null; }
        }

        private static HttpWebRequest CreateRequest(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
            req.Accept = "image/avif,image/webp,image/apng,image/*,*/*;q=0.8";
            req.Timeout = 20000;
            req.ReadWriteTimeout = 30000;
            req.AllowAutoRedirect = true;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return req;
        }

        private static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ----------------------------------------------------------- PLACEHOLDER

        private static void MakePlaceholder(Game g)
        {
            string tmp = PlaceholderPath(g) + ".tmp.png";
            try
            {
                using (Bitmap bmp = new Bitmap(CoverW, CoverH))
                using (Graphics gfx = Graphics.FromImage(bmp))
                {
                    gfx.SmoothingMode = SmoothingMode.AntiAlias;
                    gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    Color brand = Theme.PlatformColor(g.Platform);
                    Color top = Theme.Blend(Color.FromArgb(26, 30, 39), brand, 0.22f);
                    Color bottom = Color.FromArgb(7, 8, 12);

                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(0, 0, CoverW, CoverH), top, bottom, 60f))
                        gfx.FillRectangle(brush, 0, 0, CoverW, CoverH);

                    // Resplandor de color detras del icono
                    Rectangle glow = new Rectangle(-120, 140, CoverW + 240, CoverW + 240);
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(glow);
                        using (PathGradientBrush pgb = new PathGradientBrush(path))
                        {
                            pgb.CenterColor = Color.FromArgb(90, brand);
                            pgb.SurroundColors = new Color[] { Color.FromArgb(0, brand) };
                            gfx.FillPath(pgb, path);
                        }
                    }

                    Image icon = LoadGameIcon(g);
                    if (icon != null)
                    {
                        int size = 300;
                        Rectangle dest = new Rectangle((CoverW - size) / 2, (CoverH - size) / 2 - 30, size, size);
                        using (GraphicsPath p = Theme.Round(dest, 46f))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                            gfx.FillPath(b, p);
                        gfx.DrawImage(icon, dest);
                        icon.Dispose();
                    }
                    else
                    {
                        string initials = Initials(g.Title);
                        using (Font f = new Font("Segoe UI", 120f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center;
                            sf.LineAlignment = StringAlignment.Center;
                            gfx.DrawString(initials, f, new SolidBrush(Color.FromArgb(235, 255, 255, 255)),
                                new RectangleF(0, 300, CoverW, 300), sf);
                        }
                    }

                    // Vineta inferior para que el texto de la tarjeta se lea bien
                    Rectangle lower = new Rectangle(0, CoverH - 420, CoverW, 420);
                    using (LinearGradientBrush shade = new LinearGradientBrush(
                        new Rectangle(0, CoverH - 420, CoverW, 420),
                        Color.FromArgb(0, 0, 0, 0), Color.FromArgb(235, 0, 0, 0), LinearGradientMode.Vertical))
                        gfx.FillRectangle(shade, lower);

                    bmp.Save(tmp, ImageFormat.Png);
                }

                if (File.Exists(PlaceholderPath(g))) File.Delete(PlaceholderPath(g));
                File.Move(tmp, PlaceholderPath(g));
            }
            catch (Exception ex)
            {
                Storage.Log("Placeholder " + g.Title + ": " + ex.Message);
                TryDelete(tmp);
            }
        }

        private static string Initials(string title)
        {
            if (string.IsNullOrEmpty(title)) return "?";
            string[] parts = title.Split(new char[] { ' ', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        // ---------------------------------------------------------------- ICONO

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy,
            IntPtr[] icons, uint[] ids, uint count, uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Image LoadGameIcon(Game g)
        {
            string exe = g.IconSource;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) exe = g.Executable;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;
            return LoadIconFile(exe);
        }

        public static Image LoadIconFile(string exe)
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;

            try
            {
                IntPtr[] handles = new IntPtr[1];
                uint[] ids = new uint[1];
                uint count = PrivateExtractIcons(exe, 0, 256, 256, handles, ids, 1, 0);
                if (count > 0 && handles[0] != IntPtr.Zero)
                {
                    try
                    {
                        using (Icon icon = Icon.FromHandle(handles[0]))
                            return icon.ToBitmap();
                    }
                    finally { DestroyIcon(handles[0]); }
                }
            }
            catch { }

            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(exe))
                    if (icon != null) return icon.ToBitmap();
            }
            catch { }

            return null;
        }
    }
}
