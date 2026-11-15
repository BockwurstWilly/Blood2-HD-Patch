using System;
using System.Windows.Forms;

namespace Blood2HD
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var dir = Game.FindInstallDir();
            if (dir == null)
            {
                MessageBox.Show(T.Get("err_nogame"), "Blood II - HD Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            foreach (var a in args)
            {
                // /keys=modern|original: apply a key layout to autoexec.cfg without opening the window (tests)
                if (a.StartsWith("/keys=", StringComparison.OrdinalIgnoreCase))
                {
                    var cfg = AutoExec.Load(dir);
                    if (!cfg.Exists)
                        return 2;
                    cfg.Apply(a.Substring(6).Equals("modern", StringComparison.OrdinalIgnoreCase) ? AutoExec.ModernLayout() : AutoExec.OriginalLayout());
                    cfg.Save();
                    return 0;
                }
                // /launch[=<logfile>]: start the game the way the Play button does, without the window (tests)
                if (a.StartsWith("/launch", StringComparison.OrdinalIgnoreCase))
                {
                    string fovLog;
                    var ps = PatchSettings.Load(dir);
                    ps.Save();
                    var game = GameStart.Run(dir, ps, out fovLog);
                    if (a.Length > 8)
                        System.IO.File.WriteAllText(a.Substring(8).Trim('"'), game.Id + Environment.NewLine + fovLog);
                    return 0;
                }
                // /cmdline=<file>: write the game's command line to a file (tests)
                if (a.StartsWith("/cmdline=", StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.WriteAllText(a.Substring(9).Trim('"'), PatchSettings.Load(dir).CommandLine());
                    return 0;
                }
            }
            var form = new LauncherForm(dir);
            // /snapshot=<folder>: render every page invisibly to PNG files (documentation, tests)
            foreach (var a in args)
            {
                if (a.StartsWith("/snapshot=", StringComparison.OrdinalIgnoreCase))
                {
                    form.Opacity = 0;
                    form.ShowInTaskbar = false;
                    form.Shown += (o, e) => form.Snapshot(a.Substring(10).Trim('"'));
                }
            }
            Application.Run(form);
            return 0;
        }
    }
}
