using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Blood2HD
{
    /// <summary>A Miles 3D sound provider as the game names it (selected by name: the game's provider ids don't match).</summary>
    class SoundProvider
    {
        public readonly string Id, MilesName;
        public SoundProvider(string id, string milesName) { Id = id; MilesName = milesName; }

        public static readonly SoundProvider[] All =
        {
            new SoundProvider("eax", "Microsoft DirectSound3D with Creative Labs EAX(TM)"),
            new SoundProvider("ds3d", "Microsoft DirectSound3D hardware support"),
            new SoundProvider("miles2d", "Miles 2D Stereo Positional Audio"),
            new SoundProvider("none", null),
        };

        public static SoundProvider Find(string id)
        {
            return All.FirstOrDefault(p => p.Id.Equals(id ?? "", StringComparison.OrdinalIgnoreCase)) ?? All[0];
        }
    }

    /// <summary>
    /// Every setting the launcher offers, read from and written to the place that owns it:
    /// BloodHD.ini (the patch's own choices, from which the game's command line is built), dgVoodoo.conf
    /// (graphics), alsoft.ini (OpenAL Soft behind DSOAL) and the logo videos.
    /// Mouse and keys live in the game's autoexec.cfg (see AutoExec).
    /// </summary>
    class PatchSettings
    {
        public const string HdIni = "BloodHD.ini";
        public const string VoodooConf = "dgVoodoo.conf";
        public const string AlsoftIni = "alsoft.ini";
        public static readonly string[] ClassicResolutions = { "640x480", "800x600", "1024x768" };
        static readonly int[] WideHeights = { 480, 600, 768 };

        /// <summary>The monitor's aspect at the given height (even width), or null for a 4:3 (or narrower) monitor.</summary>
        public static string WideResolution(int height)
        {
            var m = Display.Current();
            if (m.Width <= 0 || m.Height <= 0 || m.Width * 3 <= m.Height * 4 + m.Height / 20)
                return null;
            int w = (int)Math.Round(height * (double)m.Width / m.Height / 2) * 2;
            return w + "x" + height;
        }

        /// <summary>Widescreen modes first (when the monitor is wide), then the original 4:3 ones.</summary>
        public static string[] GameResolutions()
        {
            return WideHeights.Select(WideResolution).Where(r => r != null).Concat(ClassicResolutions).ToArray();
        }

        public static bool IsWide(string res)
        {
            var r = ParseResolution(res);
            return r[0] * 3 > r[1] * 4 + 8;
        }

        public string Dir;

        // display
        // the original picture by default; widescreen (Hor+, see GameStart) is offered in the launcher
        public string GameResolution = "640x480";
        public string Mode = "fullscreen";      // fullscreen | windowed
        public int Msaa = 8;                    // 0, 2, 4, 8
        public int Anisotropy = 16;             // 0 = as the game asks
        public bool VSync = true;
        // the game logic runs per frame and was made for 60 fps: always capped there
        public const int FpsLimit = 60;
        // field of view in degrees as a 4:3 picture would get it (the game's own is 90); widescreen adds to the sides
        public int Fov = 90;
        public const int FovMin = 70, FovMax = 90;

        /// <summary>The horizontal FOV the player actually sees at the given resolution.</summary>
        public static double VisibleFov(int fov, string resolution)
        {
            var r = ParseResolution(resolution);
            return 2 * Math.Atan(Math.Tan(fov * Math.PI / 360) * r[0] * 3.0 / (r[1] * 4.0)) * 180 / Math.PI;
        }

        // sound
        public string Sound = "eax";            // SoundProvider id
        public string Hrtf = "auto";            // auto | true | false

        // game
        public bool Nightmares = true;
        public bool SkipLogos = true;

        // mouse
        public bool InputWrapper = true;        // dinputto8: the game's DirectInput 1-7 calls go through DirectInput 8

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static PatchSettings Load(string dir)
        {
            var s = new PatchSettings { Dir = dir };
            var hd = IniDoc.Load(Path.Combine(dir, HdIni));
            s.GameResolution = hd.Get("Display", "GameResolution", s.GameResolution);
            s.Mode = hd.Get("Display", "Mode", s.Mode).ToLowerInvariant();
            s.Msaa = ParseInt(hd.Get("Display", "Msaa"), s.Msaa);
            s.Anisotropy = ParseInt(hd.Get("Display", "Anisotropy"), s.Anisotropy);
            s.VSync = ParseInt(hd.Get("Display", "VSync"), 1) != 0;
            s.Fov = Math.Max(FovMin, Math.Min(FovMax, ParseInt(hd.Get("Display", "Fov"), s.Fov)));
            s.Sound = SoundProvider.Find(hd.Get("Sound", "Provider", s.Sound)).Id;
            s.Hrtf = hd.Get("Sound", "Hrtf", s.Hrtf).ToLowerInvariant();
            s.Nightmares = ParseInt(hd.Get("Game", "Nightmares"), 1) != 0 && File.Exists(Path.Combine(dir, Game.NightmaresRez));
            s.SkipLogos = LogosHidden(dir);
            s.InputWrapper = File.Exists(Path.Combine(dir, InputWrapperDll));
            return s;
        }

        public void Save()
        {
            var hd = IniDoc.Load(Path.Combine(Dir, HdIni));
            hd.Set("Display", "GameResolution", GameResolution);
            hd.Set("Display", "Mode", Mode);
            hd.Set("Display", "Msaa", Msaa.ToString(Inv));
            hd.Set("Display", "Anisotropy", Anisotropy.ToString(Inv));
            hd.Set("Display", "VSync", VSync ? "1" : "0");
            hd.Set("Display", "Fov", Fov.ToString(Inv));
            hd.Set("Sound", "Provider", Sound);
            hd.Set("Sound", "Hrtf", Hrtf);
            hd.Set("Game", "Nightmares", Nightmares ? "1" : "0");
            hd.Save();
            WriteVoodoo();
            WriteAlsoft();
            SetLogosHidden(Dir, SkipLogos);
            SetInputWrapper(Dir, InputWrapper);
        }

        /// <summary>dgVoodoo2: Direct3D 6 -> 11, rendered at the monitor's resolution (4:3 game resolutions with bars).</summary>
        void WriteVoodoo()
        {
            var v = IniDoc.Load(Path.Combine(Dir, VoodooConf));
            bool full = Mode != "windowed";
            v.Set("General", "FullScreenMode", full ? "true" : "false");
            v.Set("General", "ScalingMode", "stretched_ar");
            v.Set("General", "KeepWindowAspectRatio", "true");
            v.Set("General", "CaptureMouse", "true");
            v.Set("GeneralExt", "FPSLimit", FpsLimit.ToString(Inv));
            // "fake" fullscreen: a borderless window over the whole monitor. Exclusive fullscreen only works
            // when the game owns the foreground at startup (else it ends up in a small offset window), and
            // switching modes with Alt+Enter freezes the picture.
            v.Set("GeneralExt", "FullscreenAttributes", full ? "fake" : "");
            v.Set("DirectX", "DisableAltEnterToToggleScreenMode", "true");
            v.Set("DirectX", "Resolution", full ? "max" : WindowScale());
            v.Set("DirectX", "Antialiasing", Msaa == 0 ? "off" : Msaa + "x");
            v.Set("DirectX", "Filtering", Anisotropy <= 1 ? "appdriven" : Anisotropy.ToString(Inv));
            v.Set("DirectX", "ForceVerticalSync", VSync ? "true" : "false");
            v.Set("DirectX", "dgVoodooWatermark", "false");
            // the game only offers the modes the driver lists: add the widescreen ones
            var wide = GameResolutions().Where(IsWide).ToList();
            if (IsWide(GameResolution) && !wide.Contains(GameResolution))
                wide.Add(GameResolution);
            v.Set("DirectXExt", "ExtraEnumeratedResolutions", wide.Count > 0 ? string.Join(", ", wide) : "");
            v.Save();
        }

        /// <summary>A window as large as fits the desktop: the game's height times a whole number.</summary>
        string WindowScale()
        {
            var cur = Display.Current();
            var res = ParseResolution(GameResolution);
            int k = Math.Max(1, (int)(cur.Height * 0.85 / res[1]));
            return k == 1 ? "unforced" : k + "x";
        }

        void WriteAlsoft()
        {
            var a = IniDoc.Load(Path.Combine(Dir, AlsoftIni));
            a.Set("general", "hrtf", Hrtf);
            a.Set("general", "stereo-mode", Hrtf == "true" ? "headphones" : "auto");
            a.Set("eax", "enable", "true");
            a.Save();
        }

        /// <summary>The engine's command line: the original launcher's archive order, plus our display and sound choices.</summary>
        public string CommandLine()
        {
            var res = ParseResolution(GameResolution);
            var args = new List<string>
            {
                "-windowtitle \"Blood II: The Chosen\"",
                "-rez SOUND.REZ", "-rez BLOOD2.REZ", "-rez custom",
            };
            foreach (var patch in new[] { "BLOOD2P.REZ", "BLOOD2P2.REZ", "BLOOD2L.REZ" })
                if (File.Exists(Path.Combine(Dir, patch)))
                    args.Add("-rez " + patch);
            if (Nightmares && File.Exists(Path.Combine(Dir, Game.NightmaresRez)))
            {
                args.Add("-rez " + Game.NightmaresRez);
                if (File.Exists(Path.Combine(Dir, "BLOOD2NM2.REZ")))
                    args.Add("-rez BLOOD2NM2.REZ");
            }
            args.Add("+multiplayer 0");
            args.Add("++RenderDll d3d.ren");
            args.Add(string.Format(Inv, "++screenwidth {0} ++screenheight {1} ++screendepth 16", res[0], res[1]));
            var p = SoundProvider.Find(Sound);
            // id 7 = "by name": the game compares the name with the providers Miles offers
            args.Add(p.MilesName == null ? "++3dsoundprovider 0" : "++3dsoundprovider 7 ++3dsoundprovidername \"" + p.MilesName + "\"");
            return string.Join(" ", args);
        }

        public static int[] ParseResolution(string s)
        {
            var p = (s ?? "").ToLowerInvariant().Split('x');
            int w, h;
            if (p.Length == 2 && int.TryParse(p[0].Trim(), out w) && int.TryParse(p[1].Trim(), out h) && w >= 320 && h >= 200)
                return new[] { w, h };
            return new[] { 640, 480 };
        }

        // ---- dinputto8: switched off by parking the dll in _HDPatch, never deleted ---------------------

        public const string InputWrapperDll = "dinput.dll";
        static string ParkedWrapper(string dir) { return Path.Combine(dir, "_HDPatch", "dinputto8.dll"); }

        public static void SetInputWrapper(string dir, bool on)
        {
            string active = Path.Combine(dir, InputWrapperDll), parked = ParkedWrapper(dir);
            string from = on ? parked : active, to = on ? active : parked;
            if (!File.Exists(from) || File.Exists(to))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Move(from, to);
        }

        // ---- logo videos: moved aside into the backup folder, never deleted (the story intro stays) --

        static string HiddenDir(string dir) { return Path.Combine(dir, Game.BackupDir, "movies"); }

        public static bool LogosHidden(string dir)
        {
            return Game.LogoMovies.Any(v => File.Exists(Path.Combine(HiddenDir(dir), v)));
        }

        public static void SetLogosHidden(string dir, bool hide)
        {
            var hidden = HiddenDir(dir);
            foreach (var v in Game.LogoMovies)
            {
                string from = Path.Combine(hide ? dir : hidden, v), to = Path.Combine(hide ? hidden : dir, v);
                if (!File.Exists(from) || File.Exists(to))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Move(from, to);
            }
            if (!hide && Directory.Exists(hidden) && !Directory.EnumerateFileSystemEntries(hidden).Any())
                Directory.Delete(hidden);
        }

        static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : fallback;
        }
    }
}
