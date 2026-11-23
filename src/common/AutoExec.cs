using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Blood2HD
{
    /// <summary>One input: keyboard (DirectInput key code) or mouse button (0 left, 1 right, 2 middle, 3 fourth).</summary>
    struct GameKey : IEquatable<GameKey>
    {
        public const byte Mouse = 0, Keyboard = 1;
        public byte Device;
        public int Code;

        public GameKey(byte device, int code) { Device = device; Code = code; }
        public static GameKey Key(int dik) { return new GameKey(Keyboard, dik); }
        public static GameKey Button(int index) { return new GameKey(Mouse, index); }

        public bool Equals(GameKey o) { return Device == o.Device && Code == o.Code; }
        public override bool Equals(object o) { return o is GameKey && Equals((GameKey)o); }
        public override int GetHashCode() { return Device << 16 | Code; }

        public const int MouseLeft = 0, MouseRight = 1, MouseMiddle = 2, Mouse4 = 3;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetKeyNameText(int lParam, StringBuilder name, int size);

        /// <summary>Name in the current Windows keyboard layout.</summary>
        public override string ToString()
        {
            if (Device == Mouse)
            {
                switch (Code)
                {
                    case MouseLeft: return T.Get("key_mouse_l");
                    case MouseRight: return T.Get("key_mouse_r");
                    case MouseMiddle: return T.Get("key_mouse_m");
                    default: return T.Get("key_mouse_n", Code + 1);
                }
            }
            // Windows abbreviates these in German ("Rück", "Leer")
            if (Code == 0x0E) return T.Get("key_backspace");
            if (Code == 0x39) return T.Get("key_space");
            return DikName(Code) ?? "#" + Code;
        }

        public static string DikName(int dik)
        {
            var sb = new StringBuilder(64);
            int lp = (dik & 0x7F) << 16 | ((dik & 0x80) != 0 ? 1 << 24 : 0);
            if (GetKeyNameText(lp, sb, sb.Capacity) <= 0)
                return null;
            var s = sb.ToString();
            // Windows names some keys in capitals ("UMSCHALT"): write them like the others
            return s.Length > 1 && s == s.ToUpperInvariant() && s.Any(char.IsLetter) ? s.Substring(0, 1) + s.Substring(1).ToLowerInvariant() : s;
        }
    }

    /// <summary>An action of the game's control setup (the AddAction names of autoexec.cfg).</summary>
    class GameAction
    {
        public readonly string Id, De, En;
        public GameAction(string id, string de, string en) { Id = id; De = de; En = en; }
        public string Name { get { return T.German ? De : En; } }
    }

    /// <summary>
    /// autoexec.cfg in the game folder - the game's settings and controls as console commands:
    ///   "Name" "Value"                                          a console variable
    ///   rangebind "##keyboard" "W" 0.000000 0.000000 "Forward"  a key (DirectInput name or "##code")
    ///   rangebind "##mouse" "Button 0" ... "Fire"               a mouse button
    ///   scale "##mouse" "##X-axis" 0.002600                     mouse speed per axis
    /// The game rewrites the file when it quits. Everything this class does not edit stays as it is.
    /// </summary>
    class AutoExec
    {
        public const string File = "autoexec.cfg";

        // the in-game control setup, without multiplayer-only actions and the ten weapon keys
        public static readonly GameAction[] Actions =
        {
            new GameAction("Forward", "Vorwärts", "Forward"),
            new GameAction("Backward", "Rückwärts", "Backward"),
            new GameAction("StrafeLeft", "Seitschritt links", "Strafe left"),
            new GameAction("StrafeRight", "Seitschritt rechts", "Strafe right"),
            new GameAction("Left", "Links drehen", "Turn left"),
            new GameAction("Right", "Rechts drehen", "Turn right"),
            new GameAction("Jump", "Springen", "Jump"),
            new GameAction("Duck", "Ducken", "Crouch"),
            new GameAction("Run", "Rennen", "Run"),
            new GameAction("RunLock", "Dauerrennen", "Run lock"),
            new GameAction("Fire", "Feuer", "Fire"),
            new GameAction("AltFire", "Zweitfeuer", "Alt fire"),
            new GameAction("Open", "Benutzen / Öffnen", "Use / open"),
            new GameAction("UseInventory", "Gegenstand benutzen", "Use item"),
            new GameAction("InventoryLeft", "Inventar links", "Inventory left"),
            new GameAction("InventoryRight", "Inventar rechts", "Inventory right"),
            new GameAction("NextWeapon", "Nächste Waffe", "Next weapon"),
            new GameAction("PrevWeapon", "Vorige Waffe", "Previous weapon"),
            new GameAction("HolsterWeapon", "Waffe wegstecken", "Holster weapon"),
            new GameAction("Proximities", "Näherungsbomben", "Proximity bombs"),
            new GameAction("Remotes", "Fernzünderbomben", "Remote bombs"),
            new GameAction("Time", "Zeitbomben", "Time bombs"),
            new GameAction("Detonate", "Zünden", "Detonate"),
            new GameAction("TurnAround", "Umdrehen", "Turn around"),
            new GameAction("LookUp", "Nach oben sehen", "Look up"),
            new GameAction("LookDown", "Nach unten sehen", "Look down"),
            new GameAction("Crosshair", "Fadenkreuz", "Crosshair"),
            new GameAction("QuickSave", "Schnellspeichern", "Quick save"),
            new GameAction("QuickLoad", "Schnellladen", "Quick load"),
            new GameAction("LevelInfo", "Missionsziele", "Objectives"),
        };

        public static GameAction Action(string id) { return Actions.First(a => a.Id == id); }

        /// <summary>Monolith's 1998 layout (arrow keys, Ctrl fires, mouse button 2 walks), as in defaults.cfg.</summary>
        public static Dictionary<string, GameKey> OriginalLayout()
        {
            return new Dictionary<string, GameKey>
            {
                { "Forward", GameKey.Key(200) }, { "Backward", GameKey.Key(208) }, { "Left", GameKey.Key(203) }, { "Right", GameKey.Key(205) },
                { "StrafeLeft", GameKey.Key(51) }, { "StrafeRight", GameKey.Key(52) }, { "Jump", GameKey.Key(30) }, { "Duck", GameKey.Key(44) },
                { "Run", GameKey.Key(42) }, { "RunLock", GameKey.Key(58) }, { "Fire", GameKey.Button(GameKey.MouseLeft) },
                { "AltFire", GameKey.Button(GameKey.MouseMiddle) }, { "Open", GameKey.Key(57) }, { "UseInventory", GameKey.Key(28) },
                { "InventoryLeft", GameKey.Key(26) }, { "InventoryRight", GameKey.Key(27) }, { "NextWeapon", GameKey.Key(40) },
                { "PrevWeapon", GameKey.Key(39) }, { "HolsterWeapon", GameKey.Key(53) }, { "Proximities", GameKey.Key(25) },
                { "Remotes", GameKey.Key(19) }, { "Time", GameKey.Key(48) }, { "Detonate", GameKey.Key(32) }, { "TurnAround", GameKey.Key(14) },
                { "LookUp", GameKey.Key(201) }, { "LookDown", GameKey.Key(209) }, { "Crosshair", GameKey.Key(23) },
                { "QuickSave", GameKey.Key(64) }, { "QuickLoad", GameKey.Key(65) }, { "LevelInfo", GameKey.Key(59) },
            };
        }

        /// <summary>Today's shooter layout: WASD, Space jumps, Ctrl crouches, E uses, F uses the item, right mouse alt fire.</summary>
        public static Dictionary<string, GameKey> ModernLayout()
        {
            var m = OriginalLayout();
            m["Forward"] = GameKey.Key(17);        // W
            m["Backward"] = GameKey.Key(31);       // S
            m["StrafeLeft"] = GameKey.Key(30);     // A
            m["StrafeRight"] = GameKey.Key(32);    // D
            m["Left"] = GameKey.Key(203);          // arrow left
            m["Right"] = GameKey.Key(205);         // arrow right
            m["Jump"] = GameKey.Key(57);           // Space
            m["Duck"] = GameKey.Key(29);           // left Ctrl
            m["Open"] = GameKey.Key(18);           // E
            m["UseInventory"] = GameKey.Key(33);   // F
            m["AltFire"] = GameKey.Button(GameKey.MouseRight);
            m["Detonate"] = GameKey.Key(34);       // G
            m["Proximities"] = GameKey.Key(25);    // P
            m["TurnAround"] = GameKey.Key(14);     // Backspace
            return m;
        }

        class Bind
        {
            public byte Device;
            public string KeyText;      // as written in the file
            public string Ranges;       // "0.000000 0.000000"
            public string Action;
            public GameKey? Key;        // null: an axis or a name we can't read - kept untouched
        }

        static readonly Regex VarLine = new Regex("^\\s*\"([^\"]+)\"\\s+\"([^\"]*)\"\\s*$");
        static readonly Regex BindLine = new Regex("^\\s*rangebind\\s+\"##(keyboard|mouse)\"\\s+\"([^\"]*)\"\\s+(\\S+\\s+\\S+)\\s+\"([^\"]*)\"\\s*$", RegexOptions.IgnoreCase);
        static readonly Regex ScaleLine = new Regex("^\\s*scale\\s+\"##mouse\"\\s+\"(?:##)?([XY])-axis\"\\s+(\\S+)\\s*$", RegexOptions.IgnoreCase);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly string path;
        readonly List<string> lines;
        readonly List<Bind> binds = new List<Bind>();

        AutoExec(string path, List<string> lines)
        {
            this.path = path;
            this.lines = lines;
            foreach (var l in lines)
            {
                var m = BindLine.Match(l);
                if (!m.Success)
                    continue;
                var b = new Bind
                {
                    Device = m.Groups[1].Value.Equals("mouse", StringComparison.OrdinalIgnoreCase) ? GameKey.Mouse : GameKey.Keyboard,
                    KeyText = m.Groups[2].Value,
                    Ranges = m.Groups[3].Value,
                    Action = m.Groups[4].Value,
                };
                b.Key = Parse(b.Device, b.KeyText);
                binds.Add(b);
            }
        }

        public bool Exists { get { return System.IO.File.Exists(path); } }

        public static AutoExec Load(string dir)
        {
            var p = Path.Combine(dir, File);
            var lines = System.IO.File.Exists(p) ? new List<string>(System.IO.File.ReadAllLines(p, Encoding.Default)) : new List<string>();
            return new AutoExec(p, lines);
        }

        // ---- key names -------------------------------------------------------------------------

        // DirectInput's English names for keys whose Windows name differs (the game writes these)
        static readonly Dictionary<string, int> DiNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Space", 57 }, { "Left Shift", 42 }, { "Right Shift", 54 }, { "Left Ctrl", 29 }, { "Right Ctrl", 157 },
            { "Left Alt", 56 }, { "Right Alt", 184 }, { "Enter", 28 }, { "Return", 28 }, { "Tab", 15 }, { "Backspace", 14 },
            { "Esc", 1 }, { "Escape", 1 }, { "Caps Lock", 58 }, { "Up", 200 }, { "Down", 208 }, { "Left", 203 }, { "Right", 205 },
            { "Insert", 210 }, { "Delete", 211 }, { "Home", 199 }, { "End", 207 }, { "Page Up", 201 }, { "Page Down", 209 },
        };

        static GameKey? Parse(byte device, string text)
        {
            var t = text.Trim();
            if (device == GameKey.Mouse)
            {
                var m = Regex.Match(t, "^(?:##)?button\\s*(\\d+)$", RegexOptions.IgnoreCase);
                return m.Success ? GameKey.Button(int.Parse(m.Groups[1].Value, Inv)) : (GameKey?)null;
            }
            int code;
            if (t.StartsWith("##") && int.TryParse(t.Substring(2), NumberStyles.Integer, Inv, out code))
                return GameKey.Key(code);
            if (DiNames.TryGetValue(t, out code))
                return GameKey.Key(code);
            if (t.Length == 1)
            {
                // letters and digits: the scan code of the US layout, as DirectInput names them
                const string row = "1234567890";
                int i = row.IndexOf(t[0]);
                if (i >= 0)
                    return GameKey.Key(2 + i);
                var letters = new Dictionary<char, int>
                {
                    { 'Q', 16 }, { 'W', 17 }, { 'E', 18 }, { 'R', 19 }, { 'T', 20 }, { 'Y', 21 }, { 'U', 22 }, { 'I', 23 }, { 'O', 24 }, { 'P', 25 },
                    { 'A', 30 }, { 'S', 31 }, { 'D', 32 }, { 'F', 33 }, { 'G', 34 }, { 'H', 35 }, { 'J', 36 }, { 'K', 37 }, { 'L', 38 },
                    { 'Z', 44 }, { 'X', 45 }, { 'C', 46 }, { 'V', 47 }, { 'B', 48 }, { 'N', 49 }, { 'M', 50 },
                };
                if (letters.TryGetValue(char.ToUpperInvariant(t[0]), out code))
                    return GameKey.Key(code);
            }
            return null;
        }

        static string Format(GameKey k)
        {
            return k.Device == GameKey.Mouse ? "##button " + k.Code : "##" + k.Code;
        }

        // ---- bindings ------------------------------------------------------------------------------

        /// <summary>The first readable key of an action, or null.</summary>
        public GameKey? Get(GameAction a)
        {
            var b = binds.FirstOrDefault(x => x.Key.HasValue && x.Action.Equals(a.Id, StringComparison.OrdinalIgnoreCase));
            return b == null ? (GameKey?)null : b.Key;
        }

        /// <summary>Text for an action's key; a bind the launcher can't read shows as written in the file.</summary>
        public string KeyText(GameAction a)
        {
            var k = Get(a);
            if (k.HasValue)
                return k.Value.ToString();
            var raw = binds.FirstOrDefault(x => x.Action.Equals(a.Id, StringComparison.OrdinalIgnoreCase));
            return raw != null ? raw.KeyText : "—";
        }

        void Set(GameAction a, GameKey k)
        {
            binds.RemoveAll(x => x.Action.Equals(a.Id, StringComparison.OrdinalIgnoreCase) && x.Key.HasValue);
            binds.Add(new Bind { Device = k.Device, KeyText = Format(k), Ranges = "0.000000 0.000000", Action = a.Id, Key = k });
        }

        /// <summary>Assign a key; an action that had it gets this action's old key (swap).</summary>
        public void Assign(GameAction a, GameKey k)
        {
            var old = Get(a);
            foreach (var other in Actions)
            {
                if (other == a)
                    continue;
                var ok = Get(other);
                if (ok.HasValue && ok.Value.Equals(k))
                {
                    if (old.HasValue)
                        Set(other, old.Value);
                    else
                        binds.RemoveAll(x => x.Action.Equals(other.Id, StringComparison.OrdinalIgnoreCase) && x.Key.HasValue);
                }
            }
            // any other bind of this key (weapon keys, multiplayer actions) would fire as well
            binds.RemoveAll(x => x.Key.HasValue && x.Key.Value.Equals(k) && !Actions.Any(o => o.Id.Equals(x.Action, StringComparison.OrdinalIgnoreCase)));
            Set(a, k);
        }

        public void Apply(Dictionary<string, GameKey> layout)
        {
            foreach (var a in Actions)
                binds.RemoveAll(x => x.Action.Equals(a.Id, StringComparison.OrdinalIgnoreCase) && x.Key.HasValue);
            foreach (var a in Actions)
            {
                GameKey k;
                if (layout.TryGetValue(a.Id, out k))
                {
                    binds.RemoveAll(x => x.Key.HasValue && x.Key.Value.Equals(k));
                    Set(a, k);
                }
            }
        }

        // ---- variables -----------------------------------------------------------------------------

        public string GetVar(string name)
        {
            foreach (var l in lines)
            {
                var m = VarLine.Match(l);
                if (m.Success && m.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return m.Groups[2].Value;
            }
            return null;
        }

        public void SetVar(string name, string value)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var m = VarLine.Match(lines[i]);
                if (m.Success && m.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = "\"" + m.Groups[1].Value + "\" \"" + value + "\"";
                    return;
                }
            }
            // new variables go before the first command, with the others
            int at = lines.FindIndex(l => !VarLine.IsMatch(l) && l.Trim().Length > 0);
            lines.Insert(at < 0 ? lines.Count : at, "\"" + name + "\" \"" + value + "\"");
        }

        public bool InvertMouse
        {
            get { return GetVar("MouseInvertYAxis") == "1"; }
            set { SetVar("MouseInvertYAxis", value ? "1" : "0"); }
        }

        /// <summary>Mouse speed: the scale of the X axis (the game sets both axes alike).</summary>
        public double MouseScale
        {
            get
            {
                foreach (var l in lines)
                {
                    var m = ScaleLine.Match(l);
                    double v;
                    if (m.Success && m.Groups[1].Value.ToUpperInvariant() == "X" && double.TryParse(m.Groups[2].Value, NumberStyles.Float, Inv, out v) && v > 0)
                        return v;
                }
                return 0.005;
            }
            set
            {
                lines.RemoveAll(l => ScaleLine.IsMatch(l));
                pendingScale = value;
            }
        }

        double? pendingScale;

        // ---- writing -------------------------------------------------------------------------------

        public void Save()
        {
            var output = new List<string>();
            bool keyboardDone = false, mouseDone = false;
            foreach (var l in lines)
            {
                if (BindLine.IsMatch(l))
                    continue;
                output.Add(l);
                var t = l.Trim().ToLowerInvariant();
                if (t.StartsWith("enabledevice") && t.Contains("\"##keyboard\"") && !keyboardDone)
                {
                    output.AddRange(BindLines(GameKey.Keyboard));
                    keyboardDone = true;
                }
                else if (t.StartsWith("enabledevice") && t.Contains("\"##mouse\"") && !mouseDone)
                {
                    output.AddRange(BindLines(GameKey.Mouse));
                    output.AddRange(ScaleLines());
                    mouseDone = true;
                }
            }
            if (!keyboardDone)
            {
                output.Add("enabledevice \"##keyboard\"");
                output.AddRange(BindLines(GameKey.Keyboard));
            }
            if (!mouseDone)
            {
                output.Add("enabledevice \"##mouse\"");
                output.AddRange(BindLines(GameKey.Mouse));
                output.AddRange(ScaleLines());
            }
            FileUtil.WriteAtomic(path, Encoding.Default.GetBytes(string.Join("\r\n", output) + "\r\n"));
        }

        IEnumerable<string> BindLines(byte device)
        {
            string dev = device == GameKey.Mouse ? "##mouse" : "##keyboard";
            return binds.Where(b => b.Device == device)
                .Select(b => "rangebind \"" + dev + "\" \"" + b.KeyText + "\" " + b.Ranges + " \"" + b.Action + "\" ");
        }

        IEnumerable<string> ScaleLines()
        {
            if (!pendingScale.HasValue)
                yield break;
            var v = pendingScale.Value.ToString("0.000000", Inv);
            yield return "scale \"##mouse\" \"##X-axis\" " + v;
            yield return "scale \"##mouse\" \"##Y-axis\" " + v;
        }
    }
}
