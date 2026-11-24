using System;
using System.Diagnostics;
using System.IO;

namespace GamesOfAll
{
    // Lanza los juegos usando el metodo oficial de cada tienda.
    internal static class GameLauncher
    {
        public static bool Launch(Game g, out string error)
        {
            error = null;
            try
            {
                if (g.Kind == GameKind.Emulated) return LaunchEmulated(g, out error);
                switch (g.Platform)
                {
                    case Platform.Steam:
                        return OpenUri("steam://rungameid/" + g.LaunchTarget, out error);
                    case Platform.Epic:
                        return LaunchEpic(g, out error);
                    case Platform.Riot:
                        return LaunchRiot(g, out error);
                    default:
                        return StartExe(g.Executable, g.LaunchArgs, g.InstallDir, out error);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Storage.Log("Error al lanzar " + g.Title + ": " + ex);
                return false;
            }
        }

        // Juego emulado: se abre el emulador con la ROM/ISO como argumento.
        private static bool LaunchEmulated(Game g, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(g.Executable) || !File.Exists(g.Executable))
            {
                error = "No se encontró el emulador. Revisa la ruta del juego.";
                return false;
            }

            string rom = g.RomPath ?? "";
            string args = g.LaunchArgs;
            if (string.IsNullOrEmpty(args)) args = "{rom}";

            if (args.IndexOf("{rom}", StringComparison.OrdinalIgnoreCase) >= 0)
                args = ReplaceNoCase(args, "{rom}", rom.Length > 0 ? "\"" + rom + "\"" : "");
            else if (rom.Length > 0 && args.IndexOf(rom, StringComparison.OrdinalIgnoreCase) < 0)
                args = (args + " \"" + rom + "\"").Trim();

            ProcessStartInfo psi = new ProcessStartInfo(g.Executable);
            psi.Arguments = args.Trim();
            psi.UseShellExecute = true;
            psi.WorkingDirectory = string.IsNullOrEmpty(g.InstallDir) || !Directory.Exists(g.InstallDir)
                ? Path.GetDirectoryName(g.Executable)
                : g.InstallDir;
            return StartProcess(psi, out error);
        }

        private static string ReplaceNoCase(string text, string search, string replacement)
        {
            int index = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return text;
            return text.Substring(0, index) + replacement + text.Substring(index + search.Length);
        }

        private static bool OpenUri(string uri, out string error)        {
            error = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(uri);
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                error = "No se pudo abrir el protocolo " + uri + ". " + ex.Message;
                return false;
            }
        }

        private static bool LaunchEpic(Game g, out string error)
        {
            error = null;
            // Metodo oficial: el lanzador de Epic resuelve DRM y actualizaciones.
            string uri = "com.epicgames.launcher://apps/" + Uri.EscapeDataString(g.LaunchTarget) +
                         "?action=launch&silent=true";
            string uriError;
            if (OpenUri(uri, out uriError)) return true;
            Storage.Log("URI de Epic fallo (" + uriError + "), probando el ejecutable directo.");
            if (StartExe(g.Executable, g.LaunchArgs, g.InstallDir, out error)) return true;
            return false;
        }

        private static bool LaunchRiot(Game g, out string error)
        {
            string riotClient = Scanner.FindRiotClient();
            if (riotClient != null)
            {
                ProcessStartInfo psi = new ProcessStartInfo(riotClient);
                psi.Arguments = "--launch-product=" + g.LaunchTarget + " --launch-patchline=live";
                psi.UseShellExecute = false;
                psi.WorkingDirectory = Path.GetDirectoryName(riotClient);
                if (StartProcess(psi, out error)) return true;
            }
            // Respaldo: el cliente del juego directamente.
            if (StartExe(g.Executable, null, g.InstallDir, out error)) return true;
            if (error == null) error = "No se encontro Riot Client ni el ejecutable del juego.";
            return false;
        }

        private static bool StartExe(string exe, string args, string workDir, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                error = "No se encontro el ejecutable del juego.";
                return false;
            }
            ProcessStartInfo psi = new ProcessStartInfo(exe);
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            psi.UseShellExecute = true;
            psi.WorkingDirectory = string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir)
                ? Path.GetDirectoryName(exe)
                : workDir;
            return StartProcess(psi, out error);
        }

        private static bool StartProcess(ProcessStartInfo psi, out string error)
        {
            error = null;
            try
            {
                Process p = Process.Start(psi);
                if (p != null) { try { p.Dispose(); } catch { } }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\""));
            }
            catch (Exception ex) { Storage.Log("Abrir carpeta: " + ex.Message); }
        }
    }
}
