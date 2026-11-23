using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace Blood2HD
{
    /// <summary>Locating and validating the GOG installation of Blood II: The Chosen (+ The Nightmare Levels).</summary>
    static class Game
    {
        public const string Exe = "Client.exe";
        public const string LauncherExe = "BLOOD2.EXE";
        public const string BackupDir = "_HDPatch_Backup";
        public const string NightmaresRez = "BLOOD2NM.REZ";
        public static readonly string[] LogoMovies = { "Monolith.smk", "Gti.smk" };

        // SHA-1 of the engine this patch was tested against
        static readonly Dictionary<string, string> TestedBuilds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "D7D8138E837443740CF5C995B568ECD437F52B5F", "Client.exe GOG 2.0 / Nightmares 1.0.233" },
        };

        // files the game cannot run without; the patch relies on each of them
        static readonly string[] Required = { Exe, "BLOOD2.REZ", "SOUND.REZ", "d3d.ren", "Mss32.dll", "Msseax.m3d" };

        public static string FindInstallDir()
        {
            foreach (var candidate in Candidates())
            {
                if (string.IsNullOrEmpty(candidate))
                    continue;
                var dir = candidate.Trim().TrimEnd('\\', '"');
                try
                {
                    if (File.Exists(Path.Combine(dir, Exe)) && File.Exists(Path.Combine(dir, "BLOOD2.REZ")))
                        return Path.GetFullPath(dir);
                }
                catch (ArgumentException)
                {
                }
            }
            return null;
        }

        static IEnumerable<string> Candidates()
        {
            // the folder this program runs from: the game (replaced launcher) or _HDPatch (uninstaller copy)
            var self = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            yield return self;
            yield return Path.GetDirectoryName(self);

            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    {
                        yield return ReadValue(root, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GOGPACKBLOOD2_is1", "InstallLocation");
                        yield return ReadValue(root, @"SOFTWARE\Monolith Productions\Blood2\1.0", "WorkingDirectory");
                    }
                }
            }
        }

        static string ReadValue(RegistryKey root, string path, string name)
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                    return key == null ? null : key.GetValue(name) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool IsRunning()
        {
            return Process.GetProcessesByName("Client").Length > 0;
        }

        public static string Sha1(string path)
        {
            using (var sha = SHA1.Create())
            using (var s = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
        }

        /// <summary>Checks that the folder holds a Blood II installation the patch can work with.</summary>
        public static GameCheck Check(string dir)
        {
            var c = new GameCheck();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                c.Problems.Add(T.Get("chk_nodir"));
                return c;
            }
            foreach (var f in Required)
                if (!File.Exists(Path.Combine(dir, f)))
                    c.Problems.Add(T.Get("chk_missing", f));
            if (c.Problems.Count > 0)
                return c;
            string tested;
            c.Tested = TestedBuilds.TryGetValue(Sha1(Path.Combine(dir, Exe)), out tested);
            c.Version = tested ?? "Client.exe (" + T.Get("chk_unknown_build") + ")";
            c.Nightmares = File.Exists(Path.Combine(dir, NightmaresRez));
            c.Ok = true;
            return c;
        }
    }

    class GameCheck
    {
        public bool Ok;
        public bool Tested;
        public bool Nightmares;
        public string Version = "";
        public readonly List<string> Problems = new List<string>();
    }
}
