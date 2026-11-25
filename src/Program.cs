using System;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // .NET Framework sin app.config puede quedarse en TLS 1.0;
            // forzamos TLS 1.2/1.3 para poder descargar las caratulas.
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; }
                catch { }
                ServicePointManager.DefaultConnectionLimit = 8;
            }
            catch { }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "GamesOfAllLauncher_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Games ya está abierto.", "Games",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    ReportError(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    ReportError(e.ExceptionObject as Exception);
                };

                float scale = 1f;
                try
                {
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                        scale = g.DpiX / 96f;
                }
                catch { }
                if (scale < 0.9f) scale = 1f;
                if (scale > 3f) scale = 3f;

                Theme.Init(scale);
                Storage.EnsureFolders();

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        private static void ReportError(Exception ex)
        {
            if (ex == null) return;
            Storage.Log("Error no controlado: " + ex);
            try
            {
                MessageBox.Show("Ha ocurrido un error inesperado:\n\n" + ex.Message +
                    "\n\nSe guardó el detalle en data\\gamesofall.log",
                    "Games", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
