using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Blood2HD
{
    struct DisplayMode
    {
        public int Width, Height, Hz;
        public override string ToString() { return Width + " × " + Height; }
    }

    /// <summary>The primary monitor's current mode - dgVoodoo renders at it in fullscreen.</summary>
    static class Display
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);

        const int ENUM_CURRENT_SETTINGS = -1;

        public static DisplayMode Current()
        {
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm);
            return new DisplayMode { Width = dm.dmPelsWidth, Height = dm.dmPelsHeight, Hz = dm.dmDisplayFrequency };
        }
    }

    /// <summary>The speaker setup Windows reports to DirectSound - OpenAL Soft (behind DSOAL) mixes for it.</summary>
    static class Speakers
    {
        [ComImport, Guid("279AFA83-4981-11CE-A521-0020AF0BE560"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDirectSound
        {
            void CreateSoundBuffer();
            void GetCaps();
            void DuplicateSoundBuffer();
            void SetCooperativeLevel();
            void Compact();
            [PreserveSig] int GetSpeakerConfig(out uint config);
        }

        [DllImport("dsound.dll")]
        static extern int DirectSoundCreate(IntPtr guid, out IDirectSound ds, IntPtr outer);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string path);

        /// <summary>Text key of the configuration (spk_stereo, spk_51, ...), or null if unknown.</summary>
        public static string Config()
        {
            try
            {
                // the launcher runs in the game folder, next to DSOAL's dsound.dll: ask Windows' own
                LoadLibrary(Path.Combine(Environment.SystemDirectory, "dsound.dll"));
                IDirectSound ds;
                uint cfg;
                if (DirectSoundCreate(IntPtr.Zero, out ds, IntPtr.Zero) != 0 || ds == null)
                    return null;
                try
                {
                    if (ds.GetSpeakerConfig(out cfg) != 0)
                        return null;
                }
                finally
                {
                    Marshal.ReleaseComObject(ds);
                }
                switch (cfg & 0xFF)
                {
                    case 1: return "spk_headphone";
                    case 2: return "spk_mono";
                    case 3: return "spk_quad";
                    case 4: return "spk_stereo";
                    case 5: return "spk_surround";
                    case 6: case 9: return "spk_51";
                    case 7: case 8: return "spk_71";
                    default: return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int pid);
    }
}
