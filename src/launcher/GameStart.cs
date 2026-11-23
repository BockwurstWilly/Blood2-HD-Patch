using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Blood2HD
{
    /// <summary>
    /// Starts Client.exe. For a widescreen game resolution the 3D view gets a wider horizontal field of
    /// view (Hor+): the game is created suspended, one copy of the camera's FOV inside Client.exe is
    /// redirected to a few bytes of our own code, then the game runs. Nothing on disk changes.
    ///
    /// The engine copies the camera's FOV into its render data at 0x40FBFD (tested build of Client.exe):
    ///     mov esi,[eax+138h] / mov [esp+0D0h],esi    ; horizontal FOV, radians
    ///     mov esi,[eax+13Ch] / mov [esp+0D4h],esi    ; vertical FOV
    /// Our code stores 2 * atan(tan(h / 2) * scale * k) instead of h, scale = (width / height) / (4 / 3),
    /// and 2 * atan(tan(v / 2) * k) instead of v; k = tan(fov / 2) for the player's FOV setting (default
    /// 90 degrees = 1). A 16:9 picture shows the 4:3 view plus extra width (Hor+), nothing stretched.
    /// It applies to every FOV the game sets (zoom, cutscenes), so they change in step.
    /// </summary>
    static class GameStart
    {
        const uint Site = 0x40FBFD;            // the first mov above
        const uint Back = 0x40FC17;            // the instruction after the second store
        const uint Cave = 0x47E300;            // unused tail of the .text section (zero-filled, executable)

        static readonly byte[] Original =
        {
            0x8B, 0xB0, 0x38, 0x01, 0x00, 0x00, 0x89, 0xB4, 0x24, 0xD0, 0x00, 0x00, 0x00,
            0x8B, 0xB0, 0x3C, 0x01, 0x00, 0x00, 0x89, 0xB4, 0x24, 0xD4, 0x00, 0x00, 0x00,
        };

        /// <summary>The factor the horizontal FOV's tangent is widened by for the picture format (1 = 4:3).</summary>
        public static double Scale(int width, int height)
        {
            return width * 3.0 / (height * 4.0);
        }

        /// <summary>Starts the game; returns its process. log receives what happened with the FOV patch.</summary>
        public static Process Run(string dir, PatchSettings s, out string log)
        {
            var res = PatchSettings.ParseResolution(s.GameResolution);
            // fov = the horizontal FOV a 4:3 picture would get (game default 90): both tangents scale by
            // tan(fov / 2) / tan(45), the horizontal one also by the picture format
            double k = Math.Tan(s.Fov * Math.PI / 360);
            double h = Scale(res[0], res[1]) * k, v = k;
            string exe = Path.Combine(dir, Game.Exe);
            var si = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFO)) };
            PROCESS_INFORMATION pi;
            if (!CreateProcess(exe, "\"" + exe + "\" " + s.CommandLine(), IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, dir, ref si, out pi))
                throw new Win32Exception();
            try
            {
                log = Math.Abs(h - 1) < 0.005 && Math.Abs(v - 1) < 0.005 ? "4:3 at 90 degrees, no FOV patch" : Patch(pi.hProcess, (float)h, (float)v);
                var p = Process.GetProcessById(pi.dwProcessId);
                ResumeThread(pi.hThread);
                return p;
            }
            catch
            {
                ResumeThread(pi.hThread);
                throw;
            }
            finally
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
        }

        // fov' = 2 * atan(tan(fov / 2) * factor), from [eax+src] to [esp+dst]
        static void Widen(System.Collections.Generic.List<byte> c, byte src, byte dst, uint half, uint factor)
        {
            c.AddRange(new byte[] { 0xD9, 0x80, src, 0x01, 0x00, 0x00 });            // fld   dword [eax+1xxh]
            c.AddRange(new byte[] { 0xD8, 0x0D }); c.AddRange(BitConverter.GetBytes(half));   // fmul  dword [half]
            c.AddRange(new byte[] { 0xD9, 0xF2 });                                   // fptan          st0 = 1, st1 = tan
            c.AddRange(new byte[] { 0xDD, 0xD8 });                                   // fstp  st0      st0 = tan
            c.AddRange(new byte[] { 0xD8, 0x0D }); c.AddRange(BitConverter.GetBytes(factor)); // fmul  dword [factor]
            c.AddRange(new byte[] { 0xD9, 0xE8 });                                   // fld1
            c.AddRange(new byte[] { 0xD9, 0xF3 });                                   // fpatan         atan(tan * factor)
            c.AddRange(new byte[] { 0xD8, 0xC0 });                                   // fadd  st0, st0
            c.AddRange(new byte[] { 0xD9, 0x9C, 0x24, dst, 0x00, 0x00, 0x00 });      // fstp  dword [esp+0Dxh]
        }

        const int CodeSize = 80, CaveSize = CodeSize + 12;

        static string Patch(IntPtr proc, float hFactor, float vFactor)
        {
            // only this exact build: anything else runs unpatched (4:3 view stretched to the width)
            var site = Read(proc, Site, Original.Length);
            var cave = Read(proc, Cave, CaveSize);
            if (site == null || cave == null || !Same(site, Original))
                return "FOV patch skipped: unknown Client.exe";
            foreach (var b in cave)
                if (b != 0)
                    return "FOV patch skipped: code cave in use";

            var c = new System.Collections.Generic.List<byte>();
            uint half = Cave + CodeSize, hf = half + 4, vf = half + 8;
            Widen(c, 0x38, 0xD0, half, hf);                                          // horizontal
            Widen(c, 0x3C, 0xD4, half, vf);                                          // vertical
            // esi (the original's scratch register) is loaded anew by the next instruction
            c.Add(0xE9); c.AddRange(BitConverter.GetBytes(unchecked((int)(Back - (Cave + (uint)c.Count + 4))))); // jmp back
            if (c.Count > CodeSize)
                throw new InvalidOperationException("FOV code too long");
            while (c.Count < CodeSize)
                c.Add(0xCC);
            c.AddRange(BitConverter.GetBytes(0.5f));
            c.AddRange(BitConverter.GetBytes(hFactor));
            c.AddRange(BitConverter.GetBytes(vFactor));

            var jump = new byte[Original.Length];
            for (int i = 0; i < jump.Length; i++)
                jump[i] = 0x90;
            jump[0] = 0xE9;
            Array.Copy(BitConverter.GetBytes(unchecked((int)(Cave - (Site + 5)))), 0, jump, 1, 4);

            if (!Write(proc, Cave, c.ToArray()) || !Write(proc, Site, jump))
                return "FOV patch failed: " + new Win32Exception().Message;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "FOV patch on, tangent factors {0:0.###} x {1:0.###}", hFactor, vFactor);
        }

        static bool Same(byte[] a, byte[] b)
        {
            for (int i = 0; i < b.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        static byte[] Read(IntPtr proc, uint addr, int n)
        {
            var buf = new byte[n];
            IntPtr got;
            return ReadProcessMemory(proc, (IntPtr)addr, buf, (IntPtr)n, out got) && got.ToInt64() == n ? buf : null;
        }

        static bool Write(IntPtr proc, uint addr, byte[] data)
        {
            uint old;
            if (!VirtualProtectEx(proc, (IntPtr)addr, (IntPtr)data.Length, PAGE_EXECUTE_READWRITE, out old))
                return false;
            IntPtr done;
            bool ok = WriteProcessMemory(proc, (IntPtr)addr, data, (IntPtr)data.Length, out done) && done.ToInt64() == data.Length;
            VirtualProtectEx(proc, (IntPtr)addr, (IntPtr)data.Length, old, out old);
            FlushInstructionCache(proc, (IntPtr)addr, (IntPtr)data.Length);
            return ok;
        }

        const uint CREATE_SUSPENDED = 4, PAGE_EXECUTE_READWRITE = 0x40;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtectEx(IntPtr proc, IntPtr addr, IntPtr size, uint prot, out uint old);
        [DllImport("kernel32.dll")]
        static extern bool FlushInstructionCache(IntPtr proc, IntPtr addr, IntPtr size);
    }
}
