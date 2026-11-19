Blood II: The Chosen + The Nightmare Levels (GOG) - HD Patch 1.2.2
=================================================================

DEUTSCH
-------
Für die GOG-Version (Blood II 2.0 + Nightmares 1.0.233) unter Windows 10/11.

Installation
  Blood2HD_Setup.exe starten. Das Setup findet das Spiel automatisch (GOG-Registry), prüft die
  Spieldateien und installiert. Danach über "Blood II - HD Launcher" (Desktop / Startmenü) oder
  wie gewohnt über GOG Galaxy starten: Der HD Launcher ersetzt den Original-Launcher BLOOD2.EXE
  (wird gesichert).
  Deinstallation: Startmenü "Blood II - HD Patch entfernen", Launcher -> Info -> "Patch
  entfernen", Windows "Apps & Features" oder das Setup erneut starten. Alle Originaldateien und
  GOGs Kompatibilitätseinträge werden wiederhergestellt; Spielstände und Einstellungen bleiben.

Grafik
  * dgVoodoo2 übersetzt Direct3D 6 nach Direct3D 11: Das 3D-Bild wird in der Auflösung des
    Monitors gerechnet (bis 4K und mehr), mit Kantenglättung (bis 8x MSAA) und anisotroper
    Texturfilterung.
  * Ab Werk läuft das Spiel wie im Original in 640 x 480 (4:3 mit Rand).
  * Breitbild (neu in 1.2, wählbar im Launcher): Das Spiel läuft im Format des Monitors mit
    480 Zeilen (z. B. 854 x 480 bei 16:9), damit HUD und Schrift ihre Originalgröße behalten. Der
    Launcher verbreitert beim Start das Sichtfeld (Hor+: mehr Sicht zur Seite, nichts gestreckt,
    auch in Zwischensequenzen und beim Zoomen). Menüs und Videos bleiben 4:3.
  * Sichtfeld: Regler im Launcher von 70° bis 90° (Original 90°), angegeben für ein 4:3-Bild;
    bei Breitbild kommt der seitliche Zusatz dazu (90° ergeben bei 16:9 etwa 106°). Mehr als 90°
    gibt es nicht: Das Waffenmodell ist dafür gebaut und wirkt darüber klein und verzerrt.
    Unter 90° zoomt auch die Waffe heran und wird am unteren Rand abgeschnitten - 90° empfohlen.
    Eigene Umsetzung, keine fremden Dateien; nur im laufenden Spiel, an der Installation ändert
    sich nichts. Die 4:3-Auflösungen (640 x 480, 800 x 600, 1024 x 768) bleiben wählbar.
    Breitbild im Launcher unter Grafik -> Spielauflösung wählen.
  * Bildrate fest auf 60 fps: Die Spiellogik läuft pro Bild. Darüber laufen Zwischensequenzen zu
    schnell (Sprachzeilen werden abgeschnitten), die Maus ruckelt, bei sehr hohen Bildraten läuft
    das ganze Spiel zu schnell. Dazu VSync, Vollbild oder Fenster.
  * Ohne Admin-Start: GOG lässt Spiel und Launcher als Administrator, im XP-Modus und ohne
    Desktopkomposition laufen. Das braucht dgVoodoo nicht; der Patch setzt stattdessen nur
    "Hohe DPI-Skalierung" (richtige Darstellung bei Windows-Skalierung über 100 %).

Ton
  * DSOAL und OpenAL Soft: DirectSound mit EAX auf heutigen Soundkarten. Blood II hat mit
    Patch 2.0 EAX-Unterstützung bekommen (damals nur mit Sound-Blaster-Karten) - der Launcher
    wählt den EAX-Provider ab Werk aus.
    Wichtig: Das Spiel wählt den 3D-Provider nur zuverlässig über den Namen; der Launcher
    übergibt ihn so (über die Provider-Nummer fällt das Spiel still auf "kein 3D-Sound" zurück).
  * Kopfhörer-3D (HRTF): automatisch bei Kopfhörern, oder fest an/aus.
  * Musik (Interactive Music Architecture) läuft unverändert aus dem Spielordner.

Maus & Tastatur
  * Mäuse mit mehr als 125 Hz Abfragerate (heute fast alle) machen den Mausblick des Originals
    kaputt. Der Patch leitet die DirectInput-Aufrufe des Spiels über dinputto8 nach DirectInput 8
    um - der Fix, den PCGamingWiki dafür nennt. Abschaltbar im Launcher unter "Maus & Tastatur".
  Tastenbelegung, Mausempfindlichkeit und "Maus umkehren" lassen sich im Launcher einstellen;
  gespeichert wird in autoexec.cfg des Spiels, das Spielmenü zeigt dieselbe Belegung.
  Vorlagen: "Modern (WASD)" - bei der Neuinstallation gesetzt (abwählbar) - und "Original (1998)".

Spiel
  * The Nightmare Levels an/aus: an = Add-on mit seinen Levels, aus = reine Kampagne mit Patch 2.0.
    Spielstände beider Varianten passen nicht zueinander.
  * Logo-Videos überspringen (Monolith, GT); das Story-Intro bleibt.
  * Sprache des Launchers: Info -> Sprache (Automatisch = Windows-Sprache, English, Deutsch).

Hinweise
  * GOG Galaxy "Dateien prüfen/reparieren" stellt den Original-Launcher wieder her -> Setup erneut
    ausführen ("Aktualisieren", Einstellungen bleiben).
  * Feineinstellungen der Grafik: Launcher -> Grafik -> "dgVoodoo-Einstellungen".
  * Startet das Spiel nicht: Funk-Headsets mit USB-Empfänger (z. B. Logitech G533) verhindern den
    Start - Empfänger beim Start abziehen, danach wieder einstecken.
  * Startet das Spiel auf dem zweiten Monitor: in autoexec.cfg "CardDesc" "\\.\DISPLAY1" setzen.
  * Bekannter Original-Fehler: Alt+Tab im Vollbild kann das Spiel abstürzen lassen.
  * Die Musik-Korrektur für Windows NT/10/11 (AM18.dll über dsound statt dslite) ist in der
    GOG-Version bereits enthalten.

ENGLISH
-------
For the GOG release (Blood II 2.0 + Nightmares 1.0.233) on Windows 10/11. Run Blood2HD_Setup.exe:
it finds the game, checks the files and installs. Start the game with "Blood II - HD Launcher" or
from GOG Galaxy as usual; the HD launcher replaces BLOOD2.EXE (backed up). Uninstall via Windows
"Apps & Features" (restores every original file and GOG's compatibility entries, keeps savegames
and settings).

Graphics: dgVoodoo2 (Direct3D 6 -> 11), 3D rendered at the monitor's resolution, up to 8x MSAA,
anisotropic filtering. By default the game runs at 640 x 480 as in the original (4:3 with bars).
Widescreen (new in 1.2, chosen in the launcher): the game runs in the monitor's
aspect at 480 lines (e.g. 854 x 480 for 16:9) so HUD and text keep their size, and the launcher
widens the field of view (Hor+, also in cutscenes and when zooming); menus and videos stay 4:3.
Field of view slider 70-90 degrees (original 90), given for a 4:3 picture; widescreen adds to the
sides (90 = about 106 at 16:9). Below 90 the weapon model is zoomed too and gets cut off at the
bottom edge - 90 recommended.
Own code, no third-party files, applied in memory only. 640 x 480, 800 x 600 and 1024 x 768 (4:3
with bars) remain available.
Fixed 60 fps cap (the game logic runs per frame: above 60 fps cutscenes race and cut
off speech, the mouse judders), VSync, fullscreen or window. No admin start: GOG's XP / admin /
no-DWM entries are replaced by high-DPI awareness only.
Sound: DSOAL + OpenAL Soft - DirectSound with EAX on any sound card; the EAX provider (added to
Blood II with patch 2.0) is selected by default, by name (the game ignores the provider id).
Optional headphone 3D (HRTF). Music runs unchanged.
Mouse & keyboard: mice polling above 125 Hz break the original mouse look; the patch routes the
game's DirectInput calls through dinputto8 to DirectInput 8 (the PCGamingWiki fix), switchable
in the launcher. Key bindings, mouse speed and invert in the launcher, stored in the game's
autoexec.cfg. Presets: "Modern (WASD)" (set on a new install) and "Original (1998)".
Game: The Nightmare Levels on/off, skip logo videos (the story intro stays).
Launcher language: About -> Language (automatic = Windows language, English, German).
Notes: unplug wireless headset USB dongles while the game starts (they stop it from launching);
second monitor: set "CardDesc" "\\.\DISPLAY1" in autoexec.cfg; Alt+Tab in fullscreen may crash
(original bug); the NT music fix (AM18.dll) is already part of the GOG release.

Technical details
-----------------
  * DDraw.dll, D3DImm.dll, dgVoodooCpl.exe: dgVoodoo2 2.87.5 (Dege); dgVoodoo.conf written by the
    launcher ([General] FullScreenMode, [GeneralExt] FPSLimit, [DirectX] Resolution=max,
    Antialiasing, Filtering, ForceVerticalSync, [DirectXExt] ExtraEnumeratedResolutions = the
    widescreen modes)
  * Widescreen FOV: the launcher creates Client.exe suspended and redirects the copy of the
    camera's horizontal FOV at 0x40FBFD to 53 bytes in the unused tail of .text (0x47E300):
    h' = 2 * atan(tan(h / 2) * k * (w / h_res) / (4 / 3)), v' = 2 * atan(tan(v / 2) * k),
    k = tan(Fov / 2) from BloodHD.ini [Display] Fov (90 = 1). Only when the
    original bytes match (tested Client.exe); result in _HDPatch\launch.log
  * dinput.dll: dinputto8 v1.1.100.0 (Elisha Riedlinger, zlib); switched off = parked as
    _HDPatch\dinputto8.dll
  * dsound.dll, dsoal-aldrv.dll: DSOAL r697 + OpenAL Soft r10852 (Chris Robinson, LGPL); alsoft.ini
  * BLOOD2.EXE: the HD launcher (original in _HDPatch_Backup). It starts Client.exe with the
    original launcher's archive order (SOUND, BLOOD2, custom, BLOOD2P, BLOOD2P2, BLOOD2NM) and
    "++3dsoundprovider 7 ++3dsoundprovidername <Miles provider name>" (id 7 = by name; the
    game's ids don't match the providers Miles offers)
  * BloodHD.ini: the launcher's own choices
  * HKCU ...\AppCompatFlags\Layers: Client.exe "~ HIGHDPIAWARE", BLOOD2.EXE none (GOG's values
    saved in _HDPatch_Backup\compat.txt and restored on uninstall)
  * Monolith.smk, Gti.smk: moved to _HDPatch_Backup\movies when skipped

Lizenz / license
----------------
  HD-Patch (c) 2026 BockwurstWilly - https://github.com/BockwurstWilly
  PolyForm Noncommercial License 1.0.0 (Lizenzen\HD-Patch-License.txt): nutzen, weitergeben und
  verändern ist frei, kommerzielle Nutzung nicht; der Hinweis auf BockwurstWilly muss bleiben.
  Kommerzielle Nutzung nur nach Absprache über GitHub.
  Free to use, share and change; no commercial use; keep the credit to BockwurstWilly.
  Commercial use only by arrangement - contact via GitHub.
  Mitgelieferte Fremd-Dateien behalten ihre Lizenzen (THIRD-PARTY.txt) / bundled third-party
  files keep their own licenses.
  1.2.2: nur die Lizenz geändert (vorher MIT) / only the license changed (was MIT).

Blood II: The Chosen (c) 1998 Monolith Productions. This patch contains no game files.
