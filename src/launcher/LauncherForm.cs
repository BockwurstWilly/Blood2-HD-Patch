using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Blood2HD
{
    class LauncherForm : Form
    {
        readonly string dir;
        readonly PatchSettings s;
        readonly GameCheck check;
        readonly AutoExec cfg;
        Bitmap banner;

        readonly Panel content = new Panel();
        readonly List<RetroButton> navButtons = new List<RetroButton>();
        readonly List<Control> pages = new List<Control>();
        readonly Label status = Theme.Label("", Theme.Small, Theme.TextDim);
        readonly Timer statusTimer = new Timer { Interval = 4000 };

        // graphics
        ComboBox cbGameRes, cbMode, cbMsaa, cbAniso;
        CheckBox chkVSync;
        // sound
        ComboBox cb3d, cbHrtf;
        // mouse & keyboard
        CheckBox chkInvert, chkWrapper;
        RetroSlider slSens, slFov;
        Label lblFov;
        Label lblSens;
        DataGridView grid;
        // game
        CheckBox chkNightmares, chkLogos;
        ComboBox cbLang;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public LauncherForm(string dir)
        {
            this.dir = dir;
            check = Game.Check(dir);
            s = PatchSettings.Load(dir);
            cfg = AutoExec.Load(dir);
            banner = Banner.Load(dir);

            Text = "Blood II: The Chosen - HD Launcher";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(880, 680);
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            BuildLayout();
            LoadValues();
            ShowPage(0);
            UpdateStatus();
            Theme.ScaleToDpi(this);
        }

        // ---- layout ------------------------------------------------------------------------------

        const int BannerH = 150, NavW = 180, BottomH = 58;

        void BuildLayout()
        {
            var head = new Panel { Bounds = new Rectangle(0, 0, ClientSize.Width, BannerH) };
            head.Paint += PaintBanner;
            Controls.Add(head);

            var nav = new Panel { Bounds = new Rectangle(0, BannerH, NavW, ClientSize.Height - BannerH - BottomH), BackColor = Theme.Back };
            nav.Paint += (o, e) =>
            {
                using (var p = new Pen(Theme.Border))
                    e.Graphics.DrawLine(p, nav.Width - 1, 0, nav.Width - 1, nav.Height);
            };
            Controls.Add(nav);
            string[] names = { T.Get("nav_gfx"), T.Get("nav_audio"), T.Get("nav_mouse"), T.Get("nav_keys"), T.Get("nav_game"), T.Get("nav_info") };
            for (int i = 0; i < names.Length; i++)
            {
                int page = i;
                var b = Theme.Button(names[i]);
                b.Font = Theme.Display(10.5f);
                b.Bounds = new Rectangle(14, 18 + i * 46, NavW - 28, 38);
                b.Click += (o, e) => ShowPage(page);
                nav.Controls.Add(b);
                navButtons.Add(b);
            }
            var motto = Theme.Label(T.Get("motto"), Theme.Display(8.5f, FontStyle.Italic), Theme.TextDim);
            motto.MaximumSize = new Size(NavW - 28, 0);
            motto.Location = new Point(14, nav.Height - 70);
            nav.Controls.Add(motto);

            content.Bounds = new Rectangle(NavW, BannerH, ClientSize.Width - NavW, ClientSize.Height - BannerH - BottomH);
            content.BackColor = Theme.Panel;
            Controls.Add(content);

            pages.Add(BuildGraphicsPage());
            pages.Add(BuildAudioPage());
            pages.Add(BuildMousePage());
            pages.Add(BuildKeysPage());
            pages.Add(BuildGamePage());
            pages.Add(BuildInfoPage());
            foreach (var p in pages)
            {
                p.Bounds = content.ClientRectangle;
                p.Visible = false;
                content.Controls.Add(p);
            }

            var bottom = new Panel { Bounds = new Rectangle(0, ClientSize.Height - BottomH, ClientSize.Width, BottomH), BackColor = Theme.Panel };
            bottom.Paint += (o, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };
            Controls.Add(bottom);
            status.AutoSize = false;
            status.AutoEllipsis = true;
            status.Bounds = new Rectangle(16, 9, 420, 18);
            bottom.Controls.Add(status);
            statusTimer.Tick += (o, e) => ShowWarning();
            bottom.Controls.Add(Theme.CreditsLabel(new Point(16, 31)));

            var play = Theme.Button(T.Get("btn_play"), true);
            play.Bounds = new Rectangle(ClientSize.Width - 168, 11, 154, 36);
            play.Click += (o, e) => Play();
            var apply = Theme.Button(T.Get("btn_apply"));
            apply.Bounds = new Rectangle(ClientSize.Width - 296, 12, 118, 34);
            apply.Click += (o, e) => { if (SaveAll() && !LanguageChanged()) SetStatus(T.Get("st_saved"), Theme.Good); };
            var close = Theme.Button(T.Get("btn_close"));
            close.Bounds = new Rectangle(ClientSize.Width - 424, 12, 118, 34);
            close.Click += (o, e) => Close();
            bottom.Controls.AddRange(new Control[] { play, apply, close });
        }

        void PaintBanner(object sender, PaintEventArgs e)
        {
            Banner.Paint(e.Graphics, ((Panel)sender).ClientRectangle, banner, "HD LAUNCHER  ·  v" + Version.Text);
        }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].Primary = i == index;
                navButtons[i].Invalidate();
            }
        }

        Panel NewPage(string title, out int y)
        {
            var page = new Panel { BackColor = Theme.Panel };
            var head = new Label
            {
                Text = title,
                Font = Theme.Display(16f),
                ForeColor = Theme.Brass,
                BackColor = Color.Transparent,
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(24, 14),
            };
            page.Controls.Add(head);
            page.Paint += (o, e) => Theme.Rule(e.Graphics, Theme.S(24), page.Width - Theme.S(24), Theme.S(50));
            y = 64;
            return page;
        }

        // every setting is one row: label left, control from ValueX; the row with the focus lights up
        const int RowX = 14, RowW = 672, RowH = 38, ValueX = 300;

        /// <summary>Height in layout units: combos and labels take theirs from the (already DPI-sized) font.</summary>
        static int LayoutHeight(Control c)
        {
            return c is ComboBox || c is Label ? Theme.Unscaled(c.Height) : c.Height;
        }

        RowPanel NewRow(Panel page, ref int y, string label, Control control)
        {
            var row = new RowPanel { Bounds = new Rectangle(RowX, y, RowW, RowH) };
            if (label != null)
            {
                var l = Theme.Label(label);
                l.Location = new Point(14, (RowH - LayoutHeight(l)) / 2);
                row.Controls.Add(l);
            }
            if (control != null)
            {
                control.Location = new Point(ValueX, (RowH - LayoutHeight(control)) / 2);
                row.Controls.Add(control);
            }
            page.Controls.Add(row);
            y += RowH + 2;
            return row;
        }

        CheckBox Toggle(Panel page, ref int y, string label)
        {
            var t = new RetroToggle();
            NewRow(page, ref y, label, t);
            return t;
        }

        // the one hint a page may have
        Label Note(Panel page, ref int y, string text)
        {
            var info = Theme.Label(text, Theme.Small, Theme.TextDim);
            info.MaximumSize = new Size(640, 0);
            info.Location = new Point(28, y + 6);
            page.Controls.Add(info);
            y += Theme.TextHeight(info, 640) + 14;
            return info;
        }

        RetroSlider Slider(Panel page, ref int y, string label, int min, int max, out Label value, int width = 260)
        {
            var sl = new RetroSlider { Minimum = min, Maximum = max, Width = width };
            var row = NewRow(page, ref y, label, sl);
            value = Theme.Label("", Theme.Bold, Theme.AccentLight);
            value.Location = new Point(ValueX + width + 12, (RowH - LayoutHeight(value)) / 2);
            row.Controls.Add(value);
            return sl;
        }

        static DataGridView Table(int columns, int rows, bool selectable, int nameWeight = 30, int valueWeight = 20)
        {
            var g = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ScrollBars = ScrollBars.None,
                RowHeadersVisible = false,
                ColumnHeadersVisible = false,
                BackgroundColor = Theme.Panel,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Theme.Border,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            };
            g.DefaultCellStyle.BackColor = Theme.Panel;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = selectable ? Theme.Accent : Theme.Panel;
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.AlternatingRowsDefaultCellStyle.BackColor = Theme.PanelLight;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = selectable ? Theme.Accent : Theme.PanelLight;
            g.RowTemplate.Height = TextRenderer.MeasureText("Ag", Theme.Small).Height + Theme.S(2);
            g.DefaultCellStyle.Font = Theme.Small;
            // two entries per line: name, value, name, value
            for (int c = 0; c < columns; c++)
            {
                g.Columns.Add("c" + c, "");
                g.Columns[c].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                g.Columns[c].FillWeight = c % 2 == 0 ? nameWeight : valueWeight;
                g.Columns[c].SortMode = DataGridViewColumnSortMode.NotSortable;
                if (c % 2 == 1)
                    g.Columns[c].DefaultCellStyle.ForeColor = Theme.AccentLight;
            }
            g.Height = Theme.Unscaled(g.RowTemplate.Height) * rows + 2;
            return g;
        }

        static ComboBox Options(int width, params string[] pairs)
        {
            var cb = Theme.Combo(width);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                cb.Items.Add(new Choice(pairs[i], pairs[i + 1]));
            return cb;
        }

        static void Select(ComboBox cb, string value)
        {
            foreach (Choice c in cb.Items)
            {
                if (string.Equals(c.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    cb.SelectedItem = c;
                    return;
                }
            }
            // value set by hand in the ini: show it rather than silently replacing it
            var custom = new Choice(value, T.Get("opt_custom", value));
            cb.Items.Add(custom);
            cb.SelectedItem = custom;
        }

        static string Value(ComboBox cb) { return ((Choice)cb.SelectedItem).Value; }

        void ShowFov()
        {
            if (slFov == null || cbGameRes.SelectedItem == null)
                return;
            var res = Value(cbGameRes);
            lblFov.Text = PatchSettings.IsWide(res)
                ? T.Get("gfx_fov_wide", slFov.Value, (int)Math.Round(PatchSettings.VisibleFov(slFov.Value, res)))
                : T.Get("gfx_fov_val", slFov.Value);
        }

        // ---- pages -------------------------------------------------------------------------------

        Control BuildGraphicsPage()
        {
            int y;
            var p = NewPage(T.Get("nav_gfx"), out y);
            var cur = Display.Current();
            NewRow(p, ref y, T.Get("gfx_monitor"), Theme.Label(T.Get("gfx_monitor_val", cur.Width, cur.Height), Theme.Bold, Theme.AccentLight));
            cbGameRes = Theme.Combo(320);
            const string recommended = "640x480";   // the original picture
            foreach (var r in PatchSettings.GameResolutions())
            {
                var wh = PatchSettings.ParseResolution(r);
                var text = T.Get(PatchSettings.IsWide(r) ? "gfx_res_wide" : "gfx_res_43", wh[0], wh[1]);
                cbGameRes.Items.Add(new Choice(r, r == recommended ? T.Get("gfx_res_rec", text) : text));
            }
            NewRow(p, ref y, T.Get("gfx_gameres"), cbGameRes);
            slFov = Slider(p, ref y, T.Get("gfx_fov"), PatchSettings.FovMin, PatchSettings.FovMax, out lblFov, 200);
            slFov.ValueChanged += (o, e) => ShowFov();
            cbGameRes.SelectedIndexChanged += (o, e) => ShowFov();
            cbMode = Options(320, "fullscreen", T.Get("gfx_mode_fs"), "windowed", T.Get("gfx_mode_win"));
            NewRow(p, ref y, T.Get("gfx_mode"), cbMode);
            cbMsaa = Options(320, "0", T.Get("gfx_off"), "2", "2x MSAA", "4", "4x MSAA", "8", "8x MSAA");
            NewRow(p, ref y, T.Get("gfx_aa"), cbMsaa);
            cbAniso = Options(320, "0", T.Get("gfx_af_app"), "4", T.Get("gfx_af_x", 4), "8", T.Get("gfx_af_x", 8), "16", T.Get("gfx_af_x", 16));
            NewRow(p, ref y, T.Get("gfx_af"), cbAniso);
            // the game logic runs per frame: above 60 fps cutscenes race and cut off speech, the mouse judders
            NewRow(p, ref y, T.Get("gfx_fps"), Theme.Label(T.Get("gfx_fps_fixed"), Theme.Bold, Theme.AccentLight));
            chkVSync = Toggle(p, ref y, T.Get("gfx_vsync"));
            var cpl = Theme.Button(T.Get("gfx_cpl"));
            cpl.Size = new Size(200, 30);
            cpl.Click += (o, e) =>
            {
                var exe = Path.Combine(dir, "dgVoodooCpl.exe");
                if (File.Exists(exe) && SaveAll())
                    Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = dir });
            };
            NewRow(p, ref y, T.Get("gfx_cpl_row"), cpl);
            // the weapon model is drawn with the same camera: a smaller FOV zooms it out of the bottom edge
            Note(p, ref y, T.Get("gfx_fov_hint"));
            return p;
        }

        Control BuildAudioPage()
        {
            int y;
            var p = NewPage(T.Get("nav_audio"), out y);
            cb3d = Options(320, "eax", T.Get("au_eax"), "ds3d", T.Get("au_ds3d"), "miles2d", T.Get("au_miles2d"), "none", T.Get("au_none"));
            NewRow(p, ref y, T.Get("au_3d"), cb3d);
            cbHrtf = Options(320, "auto", T.Get("au_hrtf_auto"), "true", T.Get("au_hrtf_on"), "false", T.Get("au_hrtf_off"));
            NewRow(p, ref y, T.Get("au_hrtf"), cbHrtf);
            // OpenAL Soft mixes for the speaker setup Windows reports
            var spk = Speakers.Config();
            var row = NewRow(p, ref y, T.Get("au_windows"), Theme.Label(spk == null ? T.Get("spk_unknown") : T.Get(spk), Theme.Bold, Theme.AccentLight));
            var open = Theme.Button(T.Get("au_open"));
            open.Bounds = new Rectangle(ValueX + 130, (RowH - 30) / 2, 190, 30);
            open.Click += (o, e) => { try { Process.Start("control.exe", "mmsys.cpl"); } catch (Exception) { } };
            row.Controls.Add(open);
            Note(p, ref y, T.Get("au_volume"));
            return p;
        }

        // mouse speed: the axis scale of autoexec.cfg, shown as 1..100 (x 0.0002)
        const double SensStep = 0.0002;

        Control BuildMousePage()
        {
            int y;
            var p = NewPage(T.Get("nav_mouse"), out y);
            slSens = Slider(p, ref y, T.Get("mo_sens"), 1, 60, out lblSens);
            slSens.ValueChanged += (o, e) => lblSens.Text = slSens.Value.ToString(Inv);
            slSens.Enabled = cfg.Exists;
            chkInvert = Toggle(p, ref y, T.Get("mo_invert"));
            chkInvert.Enabled = cfg.Exists;
            chkWrapper = Toggle(p, ref y, T.Get("mo_wrapper"));
            if (!cfg.Exists)
                Note(p, ref y, T.Get("keys_nofile"));
            return p;
        }

        Control BuildKeysPage()
        {
            int y;
            var p = NewPage(T.Get("nav_keys"), out y);
            var hint = Note(p, ref y, cfg.Exists ? T.Get("keys_hint") : T.Get("keys_nofile"));
            if (!cfg.Exists)
                hint.ForeColor = Theme.Warn;
            grid = Table(4, Half, true);
            grid.Location = new Point(RowX, y);
            grid.Width = RowW;
            grid.Enabled = cfg.Exists;
            grid.CellDoubleClick += (o, e) => EditKey(ActionAt(e.RowIndex, e.ColumnIndex));
            grid.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter && grid.CurrentCell != null) { EditKey(ActionAt(grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex)); e.Handled = true; }
            };
            p.Controls.Add(grid);

            int by = grid.Bottom + 10;
            var edit = Theme.Button(T.Get("keys_edit"));
            edit.Bounds = new Rectangle(RowX, by, 140, 30);
            edit.Click += (o, e) => { if (grid.CurrentCell != null) EditKey(ActionAt(grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex)); };
            var original = Theme.Button(T.Get("keys_original"));
            original.Bounds = new Rectangle(RowX + RowW - 152, by, 152, 30);
            original.Click += (o, e) => { cfg.Apply(AutoExec.OriginalLayout()); FillKeys(); };
            var modern = Theme.Button(T.Get("keys_modern"));
            modern.Bounds = new Rectangle(original.Left - 168, by, 160, 30);
            modern.Click += (o, e) => { cfg.Apply(AutoExec.ModernLayout()); FillKeys(); };
            foreach (var b in new[] { edit, modern, original })
                b.Enabled = cfg.Exists;
            p.Controls.AddRange(new Control[] { edit, modern, original });
            return p;
        }

        Control BuildGamePage()
        {
            int y;
            var p = NewPage(T.Get("nav_game"), out y);
            chkNightmares = Toggle(p, ref y, check.Nightmares ? T.Get("ga_nightmares") : T.Get("ga_nightmares_missing"));
            chkNightmares.Enabled = check.Nightmares;
            chkLogos = Toggle(p, ref y, T.Get("ga_logos"));
            // language: the window is rebuilt in the new language (settings are saved first)
            cbLang = Options(320, "auto", T.Get("lang_auto"), "de", "Deutsch", "en", "English");
            NewRow(p, ref y, T.Get("ga_lang"), cbLang);
            return p;
        }

        Control BuildInfoPage()
        {
            int y;
            var p = NewPage(T.Get("nav_info"), out y);
            var head = Theme.Label(T.Get("info_line"), Theme.Bold, Theme.Text);
            head.MaximumSize = new Size(640, 0);
            head.Location = new Point(28, y + 4);
            p.Controls.Add(head);
            y += Theme.TextHeight(head, 640) + 14;
            var text = Theme.Label(T.Get("info_components"));
            text.MaximumSize = new Size(640, 0);
            text.Location = new Point(28, y);
            p.Controls.Add(text);
            y += Theme.TextHeight(text, 640) + 16;

            var open = Theme.Button(T.Get("info_open"));
            open.Bounds = new Rectangle(28, y, 200, 30);
            open.Click += (o, e) => Process.Start("explorer.exe", "\"" + dir + "\"");
            var readme = Theme.Button(T.Get("info_readme"));
            readme.Bounds = new Rectangle(236, y, 200, 30);
            readme.Click += (o, e) =>
            {
                var r = Path.Combine(dir, @"_HDPatch\README.txt");
                if (File.Exists(r)) Process.Start("notepad.exe", "\"" + r + "\"");
            };
            var remove = Theme.Button(T.Get("info_uninstall"));
            remove.Bounds = new Rectangle(444, y, 200, 30);
            remove.Click += (o, e) =>
            {
                // the uninstaller replaces this launcher with the original: start it, then get out of its way
                var u = Path.Combine(dir, PatchDir, "Blood2HD_Setup.exe");
                if (!File.Exists(u))
                    return;
                Process.Start(new ProcessStartInfo(u, "/uninstall") { WorkingDirectory = dir });
                Close();
            };
            p.Controls.AddRange(new Control[] { open, readme, remove });

            // small at the bottom: version, game build, folder
            int pageH = 680 - BannerH - BottomH;
            var state = !check.Ok ? T.Get("info_problems") + " " + string.Join(" | ", check.Problems)
                : check.Version + (check.Tested ? "" : " – " + T.Get("info_untested"));
            var small = Theme.Label(T.Get("info_small", Version.Text, state, dir), Theme.Small, check.Ok ? Theme.TextDim : Theme.Warn);
            small.MaximumSize = new Size(640, 0);
            int smallH = Theme.TextHeight(small, 640);
            small.Location = new Point(28, pageH - smallH - 12);
            p.Controls.Add(small);

            // BockwurstWilly Labs, centred between the buttons and the small print
            int top = open.Bottom + 12, ls = Math.Min(112, small.Top - top - 10);
            if (ls >= 56)
            {
                var logo = Theme.Logo(new Rectangle((remove.Right + open.Left - ls) / 2, top, ls, ls), Theme.Panel, "res/bockwurstwilly.png");
                if (logo != null)
                    p.Controls.Add(logo);
            }
            return p;
        }

        const string PatchDir = "_HDPatch";

        // ---- values --------------------------------------------------------------------------------

        void LoadValues()
        {
            Select(cbGameRes, s.GameResolution);
            Select(cbMode, s.Mode);
            Select(cbMsaa, s.Msaa.ToString(Inv));
            Select(cbAniso, s.Anisotropy <= 1 ? "0" : s.Anisotropy.ToString(Inv));
            chkVSync.Checked = s.VSync;
            slFov.Value = s.Fov;
            ShowFov();

            Select(cb3d, s.Sound);
            Select(cbHrtf, s.Hrtf);

            slSens.Value = Math.Max(1, (int)Math.Round(cfg.MouseScale / SensStep));
            lblSens.Text = slSens.Value.ToString(Inv);
            chkInvert.Checked = cfg.InvertMouse;
            chkWrapper.Checked = s.InputWrapper;
            FillKeys();

            chkNightmares.Checked = s.Nightmares;
            chkLogos.Checked = s.SkipLogos;
            Select(cbLang, T.Language);
        }

        void TakeValues()
        {
            s.GameResolution = Value(cbGameRes);
            s.Mode = Value(cbMode);
            s.Msaa = int.Parse(Value(cbMsaa), Inv);
            s.Anisotropy = int.Parse(Value(cbAniso), Inv);
            s.VSync = chkVSync.Checked;
            s.Fov = slFov.Value;

            s.Sound = Value(cb3d);
            s.Hrtf = Value(cbHrtf);

            if (cfg.Exists)
            {
                // only write the speed when it was moved: the game's own slider keeps its exact value otherwise
                if (slSens.Value != Math.Max(1, (int)Math.Round(cfg.MouseScale / SensStep)))
                    cfg.MouseScale = slSens.Value * SensStep;
                cfg.InvertMouse = chkInvert.Checked;
            }

            s.InputWrapper = chkWrapper.Checked;
            s.Nightmares = chkNightmares.Checked;
            s.SkipLogos = chkLogos.Checked;
            // stored with the other settings (also when the player starts the game right away)
            try { T.SaveLanguage(dir, Value(cbLang)); } catch (Exception) { }
        }

        /// <summary>A new launcher language takes a restart: the window is built once, in one language.</summary>
        bool LanguageChanged()
        {
            if (Value(cbLang) == T.Language)
                return false;
            Application.Restart();
            return true;
        }

        int Half { get { return (AutoExec.Actions.Length + 1) / 2; } }

        int ActionAt(int row, int col)
        {
            if (row < 0 || col < 0)
                return -1;
            int i = row + (col >= 2 ? Half : 0);
            return i < AutoExec.Actions.Length ? i : -1;
        }

        void FillKeys()
        {
            var keep = grid.CurrentCell != null ? new Point(grid.CurrentCell.ColumnIndex, grid.CurrentCell.RowIndex) : Point.Empty;
            grid.Rows.Clear();
            var all = AutoExec.Actions;
            for (int r = 0; r < Half; r++)
            {
                var b = r + Half < all.Length ? all[r + Half] : null;
                grid.Rows.Add(all[r].Name, cfg.KeyText(all[r]), b != null ? b.Name : "", b != null ? cfg.KeyText(b) : "");
            }
            if (grid.Rows.Count > 0)
                grid.CurrentCell = grid.Rows[Math.Min(keep.Y, grid.Rows.Count - 1)].Cells[keep.X];
        }

        void EditKey(int index)
        {
            if (index < 0 || !cfg.Exists)
                return;
            var a = AutoExec.Actions[index];
            using (var dlg = new KeyBindDialog(a))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result.HasValue)
                {
                    cfg.Assign(a, dlg.Result.Value);
                    FillKeys();
                }
            }
        }

        // ---- actions -------------------------------------------------------------------------------

        bool SaveAll()
        {
            if (Game.IsRunning())
            {
                MessageBox.Show(this, T.Get("err_running"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            try
            {
                TakeValues();
                s.Save();
                if (cfg.Exists)
                    cfg.Save();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, T.Get("err_save", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void Play()
        {
            if (!SaveAll())
                return;
            try
            {
                // The game needs the foreground at startup. Let it take it, and only hide and close once
                // its window is in front - going away earlier hands the focus to whatever window was before.
                NativeMethods.AllowSetForegroundWindow(-1);
                string fovLog;
                var game = GameStart.Run(dir, s, out fovLog);
                try { File.WriteAllText(Path.Combine(dir, PatchDir, "launch.log"), DateTime.Now.ToString("s") + "  " + s.GameResolution + "  " + fovLog + Environment.NewLine); }
                catch (Exception) { }
                Enabled = false;
                var until = DateTime.Now.AddSeconds(20);
                while (game != null && !game.HasExited && DateTime.Now < until)
                {
                    game.Refresh();
                    if (game.MainWindowHandle != IntPtr.Zero)
                    {
                        NativeMethods.SetForegroundWindow(game.MainWindowHandle);
                        break;
                    }
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                Close();
            }
            catch (Exception ex)
            {
                Enabled = true;
                MessageBox.Show(this, T.Get("err_start", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // a warning about the installation stays; everything else is a short confirmation
        string warning = "";

        void UpdateStatus()
        {
            if (!check.Ok)
                warning = T.Get("st_problem");
            else if (!File.Exists(Path.Combine(dir, "DDraw.dll")) || !File.Exists(Path.Combine(dir, "dsound.dll")))
                warning = T.Get("st_notinstalled");
            else
                warning = "";
            ShowWarning();
        }

        void ShowWarning()
        {
            statusTimer.Stop();
            status.Text = warning;
            status.ForeColor = Theme.Warn;
        }

        void SetStatus(string text, Color color)
        {
            status.Text = text;
            status.ForeColor = color;
            statusTimer.Stop();
            statusTimer.Start();
        }

        public void Snapshot(string folder)
        {
            Directory.CreateDirectory(folder);
            string[] names = { "graphics", "sound", "mouse", "keys", "game", "about" };
            for (int i = 0; i < pages.Count; i++)
            {
                ShowPage(i);
                Refresh();
                // only the window's content, without the Windows title bar and border (screenshots for GitHub / the website)
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    var origin = PointToScreen(Point.Empty);
                    var client = new Rectangle(origin.X - Left, origin.Y - Top, ClientSize.Width, ClientSize.Height);
                    using (var c = bmp.Clone(client, bmp.PixelFormat))
                        c.Save(Path.Combine(folder, "launcher_" + names[i] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (banner != null)
                banner.Dispose();
            base.OnFormClosed(e);
        }
    }

    class Choice
    {
        public readonly string Value, Label;
        public Choice(string value, string label) { Value = value; Label = label; }
        public override string ToString() { return Label; }
    }
}
