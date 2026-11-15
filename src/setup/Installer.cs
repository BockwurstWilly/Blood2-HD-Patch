using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Blood2HD
{
    class InstallOptions
    {
        public bool DesktopShortcut = true;
        public bool StartMenuShortcut = true;
        public bool SkipLogos = true;
        public bool ModernKeys = true;
    }

    /// <summary>Install / uninstall. Every step logs; any exception aborts before the next step.</summary>
    class Installer
    {
        public const string PatchDir = "_HDPatch";
        public const string SetupCopy = "Blood2HD_Setup.exe";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Blood2HDPatch";
        public const string ShortcutName = "Blood II - HD Launcher.lnk";
        // start menu entry that runs the uninstaller (both languages, so either one is removed again)
        static readonly string[] UninstallShortcuts = { "Blood II – HD Patch entfernen.lnk", "Blood II - Remove HD Patch.lnk" };
        const string Resource = "payload/";
        const string CompatFile = "compat.txt";
        const string CompatKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        // files of the GOG release the patch replaces; backed up before the first install
        static readonly string[] Replaced = { Game.LauncherExe, PatchSettings.InputWrapperDll };
        // files the patch adds (removed again on uninstall, unless restored from the backup)
        static readonly string[] Added =
        {
            "DDraw.dll", "D3DImm.dll", "dinput.dll", "dgVoodoo.conf", "dgVoodooCpl.exe", "dsound.dll", "dsoal-aldrv.dll", "alsoft.ini",
            "vcruntime140.dll", "msvcp140.dll", "msvcp140_atomic_wait.dll", PatchSettings.HdIni, ShortcutName,
        };
        // settings files an update must not reset
        static readonly string[] UserSettings = { PatchSettings.VoodooConf, PatchSettings.AlsoftIni };

        // GOG's compatibility entries make the game and its launcher start as administrator, with XP SP2 and
        // without desktop composition. dgVoodoo needs none of that; the new launcher must not ask for admin rights.
        static readonly Dictionary<string, string> CompatLayers = new Dictionary<string, string>
        {
            { Game.Exe, "~ HIGHDPIAWARE" },
            { Game.LauncherExe, null },
        };

        readonly Action<string> log;
        public Installer(Action<string> log) { this.log = log; }

        public static bool IsInstalled(string dir)
        {
            return dir != null && File.Exists(Path.Combine(dir, Game.BackupDir, Game.LauncherExe));
        }

        // ---- install ---------------------------------------------------------------------------------

        public void Install(string dir, InstallOptions o)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException(T.Get("err_running"));
            var check = Game.Check(dir);
            if (!check.Ok)
                throw new InvalidOperationException(T.Get("err_check") + "\n" + string.Join("\n", check.Problems));
            bool update = IsInstalled(dir);

            // 1. back up the GOG originals and compatibility entries (only once: an update keeps the backup)
            var backup = Path.Combine(dir, Game.BackupDir);
            if (!update)
            {
                foreach (var name in Replaced)
                    BackUp(dir, backup, name);
                SaveCompat(dir, Path.Combine(backup, CompatFile));
                log(T.Get("log_backup", backup));
            }
            else
            {
                log(T.Get("log_update"));
            }

            // 2. payload: game\... into the game folder, doc\... into _HDPatch
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames().Where(r => r.StartsWith(Resource)).OrderBy(r => r))
            {
                var rel = res.Substring(Resource.Length).Replace('/', '\\');
                string target;
                if (rel.StartsWith(@"doc\"))
                    target = Path.Combine(dir, PatchDir, rel.Substring(4));
                else if (rel.StartsWith(@"game\"))
                    target = Path.Combine(dir, rel.Substring(5));
                else
                    continue;
                var name = Path.GetFileName(target);
                // an update must not switch the input wrapper back on when the player parked it
                if (update && name.Equals(PatchSettings.InputWrapperDll, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Path.Combine(dir, PatchDir, "dinputto8.dll")))
                    target = Path.Combine(dir, PatchDir, "dinputto8.dll");
                if (update && UserSettings.Contains(name, StringComparer.OrdinalIgnoreCase) && File.Exists(target))
                {
                    log(T.Get("log_keep", name));
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var s = asm.GetManifestResourceStream(res))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    FileUtil.WriteAtomic(target, ms.ToArray());
                }
                Unblock(target);
                log(T.Get("log_copy", rel.StartsWith(@"doc\") ? PatchDir + "\\" + rel.Substring(4) : name));
            }

            // 3. compatibility entries
            foreach (var e in CompatLayers)
                SetCompat(Path.Combine(dir, e.Key), e.Value);
            log(T.Get("log_compat"));

            // 4. settings: a fresh install writes the patch's defaults, an update rewrites the user's own choices
            var settings = PatchSettings.Load(dir);
            if (!update)
                settings.SkipLogos = o.SkipLogos;
            settings.Save();
            log(T.Get("log_settings"));
            if (settings.SkipLogos)
                log(T.Get("log_logos"));
            var cfg = AutoExec.Load(dir);
            if (!update && o.ModernKeys && cfg.Exists)
            {
                cfg.Apply(AutoExec.ModernLayout());
                cfg.Save();
                log(T.Get("log_keys"));
            }

            // 5. uninstaller copy, shortcuts, Apps & Features entry
            var self = Assembly.GetExecutingAssembly().Location;
            var copy = Path.Combine(dir, PatchDir, SetupCopy);
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, copy, true);
            var launcher = Path.Combine(dir, Game.LauncherExe);
            Shortcut.Create(Path.Combine(dir, ShortcutName), launcher, dir, launcher);
            if (o.StartMenuShortcut)
            {
                Shortcut.Create(Path.Combine(StartMenuDir(), ShortcutName), launcher, dir, launcher);
                Shortcut.Create(Path.Combine(StartMenuDir(), T.Get("lnk_uninstall") + ".lnk"), copy, dir, launcher, "/uninstall");
            }
            if (o.DesktopShortcut)
                Shortcut.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName), launcher, dir, launcher);
            log(T.Get("log_shortcuts"));
            Register(dir);
            log(T.Get("log_done"));
        }

        static void BackUp(string dir, string backup, string name)
        {
            var src = Path.Combine(dir, name);
            if (!File.Exists(src))
                return;
            var dst = Path.Combine(backup, name);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
        }

        static string StartMenuDir()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        }

        static void Register(string dir)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "Blood II: The Chosen - HD Patch");
                k.SetValue("DisplayVersion", Version.Text);
                k.SetValue("Publisher", "CompiWare");
                k.SetValue("InstallLocation", dir);
                k.SetValue("DisplayIcon", Path.Combine(dir, Game.LauncherExe));
                k.SetValue("UninstallString", "\"" + Path.Combine(dir, PatchDir, SetupCopy) + "\" /uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", 6000, RegistryValueKind.DWord);
            }
        }

        // ---- compatibility entries (HKCU, the same as Properties > Compatibility) -------------------

        /// <summary>Value names are paths; Windows compares them without case, GOG wrote them in capitals.</summary>
        static string FindCompatName(RegistryKey k, string path)
        {
            return k.GetValueNames().FirstOrDefault(n => string.Equals(n, path, StringComparison.OrdinalIgnoreCase));
        }

        static void SaveCompat(string dir, string file)
        {
            var lines = new List<string>();
            using (var k = Registry.CurrentUser.OpenSubKey(CompatKey))
            {
                foreach (var exe in CompatLayers.Keys)
                {
                    var name = k == null ? null : FindCompatName(k, Path.Combine(dir, exe));
                    lines.Add(exe + "|" + (name == null ? "" : name + "|" + k.GetValue(name)));
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllLines(file, lines, Encoding.UTF8);
        }

        static void SetCompat(string path, string layers)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(CompatKey))
            {
                var name = FindCompatName(k, path);
                if (name != null)
                    k.DeleteValue(name, false);
                if (layers != null)
                    k.SetValue(path, layers);
            }
        }

        static void RestoreCompat(string dir, string file)
        {
            if (!File.Exists(file))
                return;
            foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
            {
                var p = line.Split('|');
                if (p[0].Length == 0)
                    continue;
                using (var k = Registry.CurrentUser.CreateSubKey(CompatKey))
                {
                    var name = FindCompatName(k, Path.Combine(dir, p[0]));
                    if (name != null)
                        k.DeleteValue(name, false);
                    if (p.Length >= 3)
                        k.SetValue(p[1], p[2]);
                }
            }
        }

        // ---- uninstall -------------------------------------------------------------------------------

        public void Uninstall(string dir)
        {
            if (Game.IsRunning())
                throw new InvalidOperationException(T.Get("err_running"));
            var backup = Path.Combine(dir, Game.BackupDir);
            if (!IsInstalled(dir))
                throw new InvalidOperationException(T.Get("err_nobackup"));

            // logo videos come back first: they live in the backup folder
            PatchSettings.SetLogosHidden(dir, false);
            foreach (var name in Added)
                Delete(Path.Combine(dir, name));
            foreach (var name in Replaced)
            {
                var b = Path.Combine(backup, name);
                if (File.Exists(b))
                {
                    CopyRetrying(b, Path.Combine(dir, name));
                    log(T.Get("log_restore", name));
                }
            }
            RestoreCompat(dir, Path.Combine(backup, CompatFile));
            // shortcuts and the Apps & Features entry are shared by name: only remove them when they belong to this folder
            DeleteShortcut(Path.Combine(StartMenuDir(), ShortcutName), dir);
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName), dir);
            foreach (var name in UninstallShortcuts)
                DeleteShortcut(Path.Combine(StartMenuDir(), name), dir);
            Directory.Delete(backup, true);
            var patch = Path.Combine(dir, PatchDir);
            try
            {
                if (Directory.Exists(patch))
                    Directory.Delete(patch, true);
            }
            catch (Exception ex)
            {
                // only the folder of a still-running uninstaller copy can be locked; the game is clean
                log(T.Get("log_leftover", patch, ex.Message));
            }
            if (RegisteredFor(dir))
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            log(T.Get("log_uninstalled"));
        }

        /// <summary>The launcher that started the uninstall may still be closing: give it a few seconds.</summary>
        static void CopyRetrying(string from, string to)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Copy(from, to, true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 25)
                        throw;
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        void DeleteShortcut(string lnk, string dir)
        {
            if (File.Exists(lnk) && IsInside(Shortcut.Target(lnk), dir))
                Delete(lnk);
        }

        static bool RegisteredFor(string dir)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                return k != null && IsInside(k.GetValue("InstallLocation") as string, dir);
        }

        static bool IsInside(string path, string dir)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            var root = Path.GetFullPath(dir).TrimEnd('\\') + '\\';
            return (Path.GetFullPath(path).TrimEnd('\\') + '\\').StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                log(T.Get("log_delete", Path.GetFileName(path)));
            }
        }

        // ---- helpers ---------------------------------------------------------------------------------

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool DeleteFile(string path);

        /// <summary>Drop the "downloaded from the internet" mark so Windows loads the DLLs without prompts.</summary>
        static void Unblock(string path)
        {
            DeleteFile(path + ":Zone.Identifier");
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                var probe = Path.Combine(dir, "_hdpatch_write_test.tmp");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsElevated()
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    static class Shortcut
    {
        public static void Create(string lnk, string target, string workDir, string icon, string arguments = "")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk));
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            try
            {
                dynamic sc = shell.CreateShortcut(lnk);
                sc.TargetPath = target;
                sc.Arguments = arguments;
                sc.WorkingDirectory = workDir;
                sc.IconLocation = icon + ",0";
                sc.Description = "Blood II: The Chosen - HD Launcher";
                sc.Save();
                Marshal.FinalReleaseComObject(sc);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static string Target(string lnk)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic sc = shell.CreateShortcut(lnk);
                string target = sc.TargetPath;
                Marshal.FinalReleaseComObject(sc);
                return target;
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
