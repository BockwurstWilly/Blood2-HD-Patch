using System.Collections.Generic;
using System.Globalization;

namespace Blood2HD
{
    static class Version
    {
        public const string Text = "1.3";
    }

    /// <summary>
    /// UI texts in English or German. "auto" (default) follows the Windows display language: German on a
    /// German Windows, English everywhere else. The launcher can fix it in BloodHD.ini [Launcher] Language.
    /// </summary>
    static class T
    {
        public static readonly bool WindowsGerman = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        public static bool German = WindowsGerman;
        public static string Language = "auto";     // auto | en | de

        public static void SetLanguage(string lang)
        {
            lang = (lang ?? "auto").Trim().ToLowerInvariant();
            Language = lang == "en" || lang == "de" ? lang : "auto";
            German = Language == "auto" ? WindowsGerman : Language == "de";
        }

        /// <summary>Reads the player's choice from BloodHD.ini in the game folder.</summary>
        public static void LoadLanguage(string dir)
        {
            try { SetLanguage(IniDoc.Load(System.IO.Path.Combine(dir, "BloodHD.ini")).Get("Launcher", "Language", "auto")); }
            catch (System.Exception) { }
        }

        public static void SaveLanguage(string dir, string lang)
        {
            var ini = IniDoc.Load(System.IO.Path.Combine(dir, "BloodHD.ini"));
            ini.Set("Launcher", "Language", lang);
            ini.Save();
        }

        public static string Get(string key, params object[] args)
        {
            string[] pair;
            if (!Texts.TryGetValue(key, out pair))
                return key;
            var s = German ? pair[0] : pair[1];
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            // game check
            { "chk_nodir", new[] { "Ordner nicht gefunden.", "Folder not found." } },
            { "chk_missing", new[] { "{0} fehlt.", "{0} is missing." } },
            { "chk_unknown_build", new[] { "unbekannter Build", "unknown build" } },

            // launcher frame
            { "on", new[] { "An", "On" } },
            { "off", new[] { "Aus", "Off" } },
            { "nav_keys", new[] { "Belegung", "Key bindings" } },
            { "gfx_cpl_row", new[] { "dgVoodoo2", "dgVoodoo2" } },
            { "ga_lang", new[] { "Sprache des Launchers", "Launcher language" } },
            { "info_line", new[] { "Blood II HD von BockwurstWilly – hohe Auflösung, EAX, Maus-Fix, Breitbild, Tastenbelegung", "Blood II HD by BockwurstWilly – high resolution, EAX, mouse fix, widescreen, key bindings" } },
            { "info_small", new[] { "v{0}  ·  {1}  ·  {2}", "v{0}  ·  {1}  ·  {2}" } },
            { "nav_gfx", new[] { "Grafik", "Graphics" } },
            { "nav_audio", new[] { "Ton", "Sound" } },
            { "nav_mouse", new[] { "Maus", "Mouse" } },
            { "nav_game", new[] { "Spiel", "Game" } },
            { "nav_info", new[] { "Info", "About" } },
            { "motto", new[] { "„Ich lebe, ich liebe, ich töte – ich bin zufrieden.“", "\"I live, I love, I slay, and I am content.\"" } },
            { "btn_play", new[] { "SPIELEN", "PLAY" } },
            { "btn_apply", new[] { "Übernehmen", "Apply" } },
            { "btn_close", new[] { "Schließen", "Close" } },
            { "opt_custom", new[] { "Eigene Einstellung ({0})", "Custom ({0})" } },

            // graphics
            { "gfx_monitor", new[] { "Bild", "Picture" } },
            { "gfx_monitor_val", new[] { "{0} × {1} (dgVoodoo2, Direct3D 11)", "{0} × {1} (dgVoodoo2, Direct3D 11)" } },
            { "gfx_res_wide", new[] { "{0} × {1}  Breitbild", "{0} × {1}  widescreen" } },
            { "gfx_res_43", new[] { "{0} × {1}  4:3 mit Rand", "{0} × {1}  4:3 with bars" } },
            { "gfx_gameres", new[] { "Spielauflösung", "Game resolution" } },
            { "gfx_res_rec", new[] { "{0} (empfohlen)", "{0} (recommended)" } },
            { "gfx_fov", new[] { "Sichtfeld", "Field of view" } },
            { "gfx_fov_hint", new[] { "Unter 90° wird die Waffe unten abgeschnitten.", "Below 90° the weapon gets cut off at the bottom." } },
            { "gfx_fov_val", new[] { "{0}°", "{0}°" } },
            { "gfx_fov_wide", new[] { "{0}°  (Breitbild {1}°)", "{0}°  (widescreen {1}°)" } },
            { "gfx_mode", new[] { "Anzeigemodus", "Display mode" } },
            { "gfx_mode_fs", new[] { "Vollbild", "Fullscreen" } },
            { "gfx_mode_win", new[] { "Fenster", "Window" } },
            { "gfx_aa", new[] { "Kantenglättung", "Anti-aliasing" } },
            { "gfx_off", new[] { "Aus", "Off" } },
            { "gfx_af", new[] { "Texturfilter", "Texture filtering" } },
            { "gfx_af_app", new[] { "Wie im Spiel eingestellt", "As set in the game" } },
            { "gfx_af_x", new[] { "{0}x anisotrop", "{0}x anisotropic" } },
            { "gfx_fps", new[] { "Bildrate", "Frame rate" } },
            { "gfx_fps_fixed", new[] { "fest 60 fps", "fixed at 60 fps" } },
            { "gfx_vsync", new[] { "VSync", "VSync" } },
            { "gfx_cpl", new[] { "Einstellungen …", "Settings …" } },

            // sound
            { "au_3d", new[] { "3D-Sound", "3D sound" } },
            { "au_eax", new[] { "EAX (empfohlen)", "EAX (recommended)" } },
            { "au_ds3d", new[] { "DirectSound3D", "DirectSound3D" } },
            { "au_miles2d", new[] { "Miles 2D-Positionierung", "Miles 2D positional" } },
            { "au_none", new[] { "Kein 3D-Sound", "No 3D sound" } },
            { "au_hrtf", new[] { "Kopfhörer-3D (HRTF)", "Headphone 3D (HRTF)" } },
            { "au_hrtf_auto", new[] { "Automatisch (bei Kopfhörern an)", "Automatic (on with headphones)" } },
            { "au_hrtf_on", new[] { "An", "On" } },
            { "au_hrtf_off", new[] { "Aus", "Off" } },
            { "au_windows", new[] { "Lautsprecher", "Speakers" } },
            { "spk_unknown", new[] { "unbekannt", "unknown" } },
            { "spk_headphone", new[] { "Kopfhörer", "Headphones" } },
            { "spk_mono", new[] { "Mono", "Mono" } },
            { "spk_quad", new[] { "Quadro (4.0)", "Quad (4.0)" } },
            { "spk_stereo", new[] { "Stereo", "Stereo" } },
            { "spk_surround", new[] { "Surround", "Surround" } },
            { "spk_51", new[] { "5.1 Surround", "5.1 surround" } },
            { "spk_71", new[] { "7.1 Surround", "7.1 surround" } },
            { "au_open", new[] { "Windows-Sound …", "Windows sound …" } },
            { "au_volume", new[] { "Lautstärken und Musik: im Spiel unter Optionen.", "Volume and music: in the game's options." } },

            // mouse & keyboard
            { "mo_sens", new[] { "Mausempfindlichkeit", "Mouse sensitivity" } },
            { "mo_invert", new[] { "Maus umkehren", "Invert mouse" } },
            { "mo_wrapper", new[] { "Maus-Fix (dinputto8)", "Mouse fix (dinputto8)" } },
            { "keys_title", new[] { "Tastenbelegung", "Key bindings" } },
            { "keys_hint", new[] { "Doppelklick, dann Taste drücken.", "Double-click, then press a key." } },
            { "keys_nofile", new[] { "autoexec.cfg fehlt – erst einmal das Spiel starten.", "autoexec.cfg is missing - start the game once first." } },
            { "keys_edit", new[] { "Ändern", "Change" } },
            { "keys_modern", new[] { "Modern (WASD)", "Modern (WASD)" } },
            { "keys_original", new[] { "Original (1998)", "Original (1998)" } },
            { "keys_press", new[] { "Taste oder Maustaste drücken.", "Press a key or mouse button." } },
            { "keys_esc", new[] { "Esc = Abbrechen", "Esc = cancel" } },
            { "key_mouse_l", new[] { "Linke Maustaste", "Left mouse button" } },
            { "key_mouse_r", new[] { "Rechte Maustaste", "Right mouse button" } },
            { "key_mouse_m", new[] { "Mittlere Maustaste", "Middle mouse button" } },
            { "key_mouse_n", new[] { "Maustaste {0}", "Mouse button {0}" } },
            { "key_backspace", new[] { "Rücktaste", "Backspace" } },
            { "key_space", new[] { "Leertaste", "Space" } },

            // game
            { "ga_nightmares", new[] { "The Nightmare Levels", "The Nightmare Levels" } },
            { "ga_nightmares_missing", new[] { "The Nightmare Levels (nicht installiert)", "The Nightmare Levels (not installed)" } },
            { "ga_logos", new[] { "Logo-Videos überspringen", "Skip logo videos" } },

            // info
            { "info_problems", new[] { "Probleme:", "Problems:" } },
            { "info_tested", new[] { "Getestete Spielversion.", "Tested game version." } },
            { "info_untested", new[] { "Nicht getestete Spielversion.", "Untested game version." } },
            { "info_components", new[] { "dgVoodoo2 von Dege (Freeware)\nDSOAL und OpenAL Soft von Chris Robinson (LGPL)\ndinputto8 von Elisha Riedlinger (zlib-Lizenz)\nDank an AlphaYellow (Widescreen Fixes) und die PCGamingWiki-Community\n\nLizenz: PolyForm Noncommercial 1.0.0  ·  © 2026 BockwurstWilly", "dgVoodoo2 by Dege (freeware)\nDSOAL and OpenAL Soft by Chris Robinson (LGPL)\ndinputto8 by Elisha Riedlinger (zlib license)\nThanks to AlphaYellow (Widescreen Fixes) and the PCGamingWiki community\n\nLicense: PolyForm Noncommercial 1.0.0  ·  © 2026 BockwurstWilly" } },
            { "lang_auto", new[] { "Automatisch", "Automatic" } },
            { "info_open", new[] { "Spielordner öffnen", "Open game folder" } },
            { "info_readme", new[] { "Liesmich", "Readme" } },
            { "info_uninstall", new[] { "HD-Patch entfernen …", "Remove HD patch …" } },

            // status / errors
            { "st_saved", new[] { "Einstellungen gespeichert.", "Settings saved." } },
            { "st_problem", new[] { "Spielversion wird nicht vollständig unterstützt – siehe Info.", "Game version not fully supported - see About." } },
            { "st_notinstalled", new[] { "HD-Patch-Dateien fehlen – Setup erneut ausführen.", "HD patch files missing - run the setup again." } },
            { "err_running", new[] { "Das Spiel läuft gerade. Bitte zuerst beenden.", "The game is running. Please quit it first." } },
            { "err_save", new[] { "Speichern fehlgeschlagen: {0}", "Saving failed: {0}" } },
            { "err_start", new[] { "Spielstart fehlgeschlagen: {0}", "Could not start the game: {0}" } },
            { "err_nogame", new[] { "Blood II: The Chosen (GOG) wurde nicht gefunden.", "Blood II: The Chosen (GOG) was not found." } },

            // setup
            { "su_title", new[] { "Blood II: The Chosen – HD Patch Setup", "Blood II: The Chosen - HD Patch Setup" } },
            { "su_folder", new[] { "Spielordner (GOG-Version)", "Game folder (GOG release)" } },
            { "su_browse", new[] { "Durchsuchen …", "Browse …" } },
            { "su_browse_desc", new[] { "Ordner von Blood II: The Chosen wählen (enthält Client.exe und BLOOD2.REZ)", "Choose the Blood II: The Chosen folder (contains Client.exe and BLOOD2.REZ)" } },
            { "su_bad", new[] { "Hier kann der Patch nicht installiert werden:", "The patch cannot be installed here:" } },
            { "su_ok_tested", new[] { "Spiel gefunden – getestete GOG-Version.", "Game found - tested GOG release." } },
            { "su_ok_untested", new[] { "Spiel gefunden – nicht getestete Version, sollte aber funktionieren.", "Game found - untested version, should work though." } },
            { "su_install", new[] { "Installieren", "Install" } },
            { "su_update", new[] { "Aktualisieren", "Update" } },
            { "su_uninstall", new[] { "Deinstallieren", "Uninstall" } },
            { "su_launch", new[] { "Launcher starten", "Start launcher" } },
            { "su_desktop", new[] { "Verknüpfung auf dem Desktop", "Desktop shortcut" } },
            { "su_startmenu", new[] { "Einträge im Startmenü", "Start menu entries" } },
            { "su_logos", new[] { "Logo-Videos überspringen (Story-Intro bleibt)", "Skip logo videos (story intro stays)" } },
            { "su_keys", new[] { "Moderne Tastenbelegung (WASD + Maus)", "Modern key bindings (WASD + mouse)" } },
            { "su_success", new[] { "HD-Patch installiert.\n\nStart über „Blood II - HD Launcher“ oder wie gewohnt über GOG Galaxy.", "HD patch installed.\n\nStart with \"Blood II - HD Launcher\" or from GOG Galaxy as usual." } },
            { "su_removed", new[] { "HD-Patch entfernt. Originaldateien wiederhergestellt, Spielstände und Einstellungen bleiben.", "HD patch removed. Original files restored, savegames and settings kept." } },
            { "su_uninstall_confirm", new[] { "HD-Patch wirklich deinstallieren?\nAlle Originaldateien werden wiederhergestellt.", "Really uninstall the HD patch?\nAll original files will be restored." } },
            { "su_note", new[] { "Nach „Dateien prüfen“ in GOG Galaxy das Setup erneut starten.", "After \"verify files\" in GOG Galaxy, run the setup again." } },
            { "lnk_uninstall", new[] { "Blood II – HD Patch entfernen", "Blood II - Remove HD Patch" } },
            { "err_check", new[] { "Die Spieldateien passen nicht:", "The game files don't match:" } },
            { "err_nobackup", new[] { "Keine Sicherung gefunden – der HD-Patch scheint nicht installiert zu sein.", "No backup found - the HD patch does not seem to be installed." } },
            { "err_nowrite", new[] { "Keine Schreibrechte für {0}.", "No write access to {0}." } },
            { "log_backup", new[] { "Originaldateien gesichert nach {0}", "Original files backed up to {0}" } },
            { "log_update", new[] { "Aktualisierung: vorhandene Sicherung wird beibehalten", "Update: keeping the existing backup" } },
            { "log_copy", new[] { "kopiert: {0}", "copied: {0}" } },
            { "log_keep", new[] { "behalten (eigene Einstellungen): {0}", "kept (your settings): {0}" } },
            { "log_compat", new[] { "Windows-Kompatibilitätseinträge angepasst (kein Adminstart, Skalierung)", "Windows compatibility entries adjusted (no admin start, scaling)" } },
            { "log_settings", new[] { "Grafik- und Toneinstellungen geschrieben", "Graphics and sound settings written" } },
            { "log_keys", new[] { "Tastenbelegung: Modern (WASD)", "Key bindings: modern (WASD)" } },
            { "log_logos", new[] { "Logo-Videos beiseitegelegt", "Logo videos moved aside" } },
            { "log_shortcuts", new[] { "Verknüpfungen angelegt", "Shortcuts created" } },
            { "log_done", new[] { "Fertig.", "Done." } },
            { "log_delete", new[] { "entfernt: {0}", "removed: {0}" } },
            { "log_saved", new[] { "gesichert: {0}", "backed up: {0}" } },
            { "log_restore", new[] { "wiederhergestellt: {0}", "restored: {0}" } },
            { "log_leftover", new[] { "Ordner {0} konnte nicht entfernt werden ({1}) – später von Hand löschen.", "Folder {0} could not be removed ({1}) - delete it later by hand." } },
            { "log_uninstalled", new[] { "HD-Patch entfernt.", "HD patch removed." } },
        };
    }
}
