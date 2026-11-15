# Machbarkeitsstudie: Source Port „Blood II: The Chosen“ (LithTech 1.0)

Stand: 29.10.2026 · Grundlage: lokale GOG-Installation (`D:\Blood II - The Chosen`) plus öffentlicher Community-Stand

---

## 0. Kurzfazit

**Machbar, aber nur als Neuimplementierung der Engine-Schicht. Einen fertigen Weg gibt es nicht.**
Der Blood-II-Spielcode (ClientShell / Object.lto) liegt als Quelltext vor. Die Engine selbst (`Client.exe`, `Server.dll`, `d3d.ren`, `soft.ren`) ist geschlossen und muss nachgebaut werden. Die kritische Arbeit liegt nicht bei Grafik oder Widescreen, sondern bei drei Punkten:

1. **Engine-API-Nachbau:** Die Schnittstellen, die der Spielcode aufruft (LT1 `ServerDE`/`ClientDE`), müssen originalgetreu nachgebaut werden, einschließlich der Physik (MoveObject, Kollision, Gravitation).
2. **Formate:** Das World-`.DAT` (v56) ist am schlechtesten dokumentiert. REZ, DTX und ABC sind beherrschbar.
3. **Lizenz:** Die Blood-II-Spielcode-Lizenz ist restriktiv und vermutlich nicht GPL-kompatibel. Das muss **vor** jeder Zeile Code geklärt werden.

**Empfehlung:** Weg 1, eine eigene C++/SDL2-Laufzeit („LT1-Runtime“). Sie implementiert die Original-Engine-API und kompiliert den Original-Spielcode unverändert dagegen. Aus der GPL-Jupiter-Engine (Weg 3) werden Formatwissen und einzelne Ideen übernommen, aber keine Codebasis. Godot (Weg 2) wird nicht empfohlen, weil 1:1-Gameplay dort Spiellogik und Physik doppelt nachbauen müsste.

Realistischer Aufwand: **18–36 Personenmonate** bis zur vollständig spielbaren Kampagne inkl. Nightmare Levels, bei 1–3 erfahrenen Entwicklern (Reverse Engineering und C++).

---

## 1. Befund der lokalen Installation

| Bestandteil | Datei(en) | Bedeutung für den Port |
|---|---|---|
| Engine-Client | `Client.exe`, `BLOOD2.EXE` (Launcher) | geschlossen, wird ersetzt |
| Engine-Server | `Server.dll`, `Blood2Sv.exe`, `B2NMSRV.EXE` | geschlossen; enthält Physik und Objektverwaltung, wird nachgebaut |
| Renderer | `d3d.ren`, `soft.ren` | Plug-in-Renderer-DLLs mit eigener (undokumentierter) Schnittstelle |
| Spielcode | in den REZ-Archiven (`ClientShell.dll`, `Object.lto`) | Quelltext existiert im SDK, wird neu kompiliert |
| Archive | `BLOOD2.REZ`, `BLOOD2P.REZ`, `BLOOD2P2.REZ` (Patches), `BLOOD2NM.REZ` (Add-on), `SOUND.REZ` | Header bestätigt: *„RezMgr Version 1 … LithTech Resource File“*. Die Archive müssen **gestapelt** werden (Basis → Patch → Add-on). |
| Audio | `Mss32.dll` + `.m3d`-Provider (A3D, EAX, DS3D) | Miles Sound System 4.x/5.x → OpenAL Soft |
| **Musik** | `Music\*`: 39 × `.sty`, 156 × `.sec`, 5 × `.dls`, 5 × `.PER`, dazu `IMUSIC25.DLL` | **DirectMusic / Interactive Music Architecture**: dynamische Musik. Das ist ein eigener großer Brocken (siehe B.4). |
| Video | `Intro.smk`, `Monolith.smk`, `Gti.smk` | Smacker → FFmpeg/libsmacker |
| Konfiguration | `autoexec.cfg`, `defaults.cfg`, `detail*.cfg` | Konsolen-Cvar-Syntax, sollte kompatibel bleiben |

Hinweis zur Ausgangslage im Briefing: Die Nightmare Levels werden im mitgelieferten Readme (`NGHTMARE.TXT`) nicht als Erster-Weltkrieg-Szenario beschrieben. Für die Technik spielt das keine Rolle. Wichtig ist nur, dass das Add-on eigenes REZ, eigenen Server und eigenes Savegame-Format mitbringt („save game files from previous versions will be incompatible“).

---

## A. Architektur & Lösungsansätze

### Weg 1: Engine-Rekonstruktion in C++ mit Original-Spielcode

**Prinzip:** Eine neue Laufzeit in C++17/20 (SDL2 oder SDL3) stellt dieselben C-Interfaces bereit, die `ClientShell.dll` und `Object.lto` von der Engine erwarten: Objekt-Erzeugung, Messages, `MoveObject`, `IntersectSegment`, Sound-, Sprite- und Model-Aufrufe, Konsole. Der Spielcode wird aus dem SDK-Quelltext mit einem modernen Compiler (MSVC/Clang/GCC, 32- und 64-Bit) gebaut und gegen diese Laufzeit gelinkt, statisch oder als Plug-in.

| Pro | Contra |
|---|---|
| Spielcode (Waffen, KI, Trigger, Balancing) bleibt **bit-identisch** in der Logik. Das ist die einzige Methode, die „1:1“ ernsthaft garantiert. | Engine-Seite (Physik, Kollision, BSP, Netzwerk, Savegames) muss per Reverse Engineering aus `Server.dll`/`Client.exe` erschlossen werden |
| Volle Kontrolle über Renderer, Timing, Input und Audio | SDK-Code von 1998 braucht Portierung: 32-Bit-Annahmen (`DDWORD`, Pointer in `DWORD`), MSVC-6-Eigenheiten, Windows-APIs im Spielcode |
| Linux nativ möglich, wenn der Spielcode von Win32 entkoppelt wird | Lizenzlage des Spielcodes (siehe unten) |
| Präzedenzfälle: OpenGothic, OpenMW, Doom- und Quake-Ports zeigen, dass das Muster trägt | Höchster Anfangsaufwand bis zum ersten sichtbaren Ergebnis |

### Weg 2: Re-Implementierung in Godot 4

**Prinzip:** GDExtension-Parser für REZ, DAT, DTX und ABC; Darstellung über Godot-Szenen und Shader; Spiellogik entweder neu in GDScript/C# oder als eingebetteter Original-Code.

| Pro | Contra |
|---|---|
| Renderer, Audio, Input, Gamepad und Plattformen gratis | Godot-Physik (Jolt/GodotPhysics) ≠ LithTech-Physik. Bewegungsgefühl, Stufenhöhen und Rutschen ändern sich. **Widerspricht dem 1:1-Ziel.** |
| Schneller erster Viewer | Wer den Original-Spielcode einbettet, muss **trotzdem** die komplette LT1-API nachbauen (= Weg 1) und zusätzlich gegen Godots Szenenmodell arbeiten |
| Gute Modding-Tools | BSP-Welten mit Lightmaps, Portalen und Sichtbarkeit passen schlecht in Godots Mesh-Modell und brauchen Sonderbehandlung |
| | Zwei Laufzeiten (Godot + LT-Emulation), doppelte Komplexität beim Debugging |

**Bewertung:** Gut für einen *Asset-Viewer* oder ein „Remake im Geist von“, ungeeignet für einen originalgetreuen Port.

### Weg 3: Fork einer LithTech-Community-Engine

Verfügbar ist vor allem [`jsj2008/lithtech`](https://github.com/jsj2008/lithtech) (Fork: [`rohit-n/lithtech`](https://github.com/rohit-n/lithtech)), ein Port der unter GPL veröffentlichten **Jupiter EX**-Engine. Blood II steht dort nur auf der Wunschliste („falls ich eine Kopie finde“). Eine Blood-II-Unterstützung existiert nicht.

| Pro | Contra |
|---|---|
| Vorhandener Rahmen: Ressourcen-Manager, Renderer, Konsole, Netzwerkcode | Jupiter liegt 4–5 Engine-Generationen nach LT1. Die API ist komplett anders (`ILTServer`/`ILTClient` vs. `ServerDE`/`ClientDE`, `LTVector` vs. `DVector`, anderes Objekt- und Message-Modell). |
| Formatverwandtschaft: DTX- und ABC-Nachfahren, REZ-Manager stammt aus derselben Linie | Andere Dateiversionen: Jupiter lädt DAT-Versionen späterer Generationen und `.LTB`/neuere ABC. Der v56-Loader müsste ohnehin neu geschrieben werden. |
| | Physik und Kollision von Jupiter verhalten sich **nicht** wie LT1, also kein 1:1 |
| | GPL-Pflicht für das Gesamtwerk, möglicher Konflikt mit der Blood-II-Code-Lizenz |
| | Bestehende Code-Altlasten (DirectX-8/9-Renderer, Windows-zentrisch) |

**Bewertung:** Als **Steinbruch** wertvoll (Formatverständnis, REZ-Code, Lösungsideen), als Basis ungeeignet. Ein Adapter von LT1-API auf Jupiter wäre am Ende aufwändiger als eine schlanke eigene Laufzeit.

### Bonus: Weg 0 als Brücke

Für Spieler, die *jetzt* spielen wollen: Wrapper wie dgVoodoo2 (D3D-Übersetzung), Widescreen-Patches und Raw-Input-Hooks. Das löst Physik- und HUD-Probleme bei hohen FPS nicht und ist **kein** Ersatz, liefert aber Referenzmaterial für Vergleichsaufnahmen.

### Empfehlung

**Weg 1**, ergänzt um gezielte Anleihen aus Weg 3:

```
┌─────────────────────────────────────────────────────────────┐
│ Original Blood II Spielcode (SDK, neu kompiliert)           │
│   ClientShell (HUD, Menüs, Waffen-Client)  Object (KI, Logik)│
├──────────────── LT1-API-Kompatibilitätsschicht ─────────────┤
│  ClientDE-Impl.                 ServerDE-Impl.               │
├─────────────────────────────────────────────────────────────┤
│ Neue Engine-Kerne                                           │
│  Resource (REZ-Stack) │ World (DAT v56, BSP) │ Physik (LT1-  │
│  Model (ABC) │ Tex (DTX) │ Netz/Save         │ treu, Fixed   │
│                                              │ Tick)         │
├─────────────────────────────────────────────────────────────┤
│ Plattform: SDL2/3 · Renderer (OpenGL 4.5 → Vulkan) ·         │
│ OpenAL Soft · DirectMusic-Ersatz · FFmpeg (Smacker)          │
└─────────────────────────────────────────────────────────────┘
```

Begründung:
- Nur hier bleibt die Spiellogik **unverändert**. Alle Abweichungen beschränken sich auf die Engine-Schicht und sind gegen das Original messbar.
- Die Engine-Schicht von LT1 ist überschaubar. Das Spiel ist von 1998, die API hat wenige hundert Funktionen.
- Klare Trennung Engine ↔ Spielcode erlaubt eine saubere Lizenzaufteilung: Engine als eigenes Open-Source-Projekt, Spielcode-Patches separat gemäß SDK-Lizenz.

### ⚠ Lizenz (Go/No-Go-Kriterium)

- Der Blood-II-Spielcode wurde von Monolith unter einer **eingeschränkten Lizenz** veröffentlicht, typischerweise nur für nichtkommerzielle Mods und nur zusammen mit dem Originalspiel. Das ist **nicht** GPL.
- Folge: GPL-Code (Jupiter) darf nicht in dasselbe Binary wie der SDK-Code gelinkt werden, sofern die SDK-Lizenz das nicht erlaubt.
- **Lösungsmuster:** Engine unter MIT/BSD/zlib, *ohne* Jupiter-Code (Jupiter nur als Referenz lesen, Clean-Room-Prinzip). Der Spielcode wird als separates Modul gebaut und vom Nutzer selbst kompiliert oder als Patch-Satz verteilt.
- Die Original-Lizenztexte aus dem SDK-Paket (ModDB: „Blood 2 source code“) müssen vor Projektstart wörtlich geprüft werden.

---

## B. Technische Knackpunkte (Deep Dive)

### B.1 Physik vs. Framerate

**Problem:** In LT1 rechnet der Server pro Engine-Frame. Spielcode und Engine-Physik integrieren mit der variablen Frame-Zeit (`GetFrameTime()`). Typische Symptome bei hohen FPS: veränderte Sprunghöhe, Reibung und Beschleunigung, Rundungsfehler bei sehr kleinen dt, Trigger, die doppelt oder gar nicht feuern, und HUD-Effekte (Flash, Fade), die pro Frame statt pro Zeit laufen.

**Lösung: Simulation und Darstellung trennen**

1. **Fester Simulationstakt** für Server-Logik *und* Engine-Physik, z. B. 60 Hz (umschaltbar 30/35/60 für A/B-Vergleiche). `GetFrameTime()` liefert dem Spielcode dann **immer** den festen Wert. Der Spielcode sieht eine perfekt stabile Maschine.
2. **Akkumulator-Schleife** (klassisch nach „Fix Your Timestep“):
   ```cpp
   accumulator += realDelta;
   while (accumulator >= TICK) { server.Update(TICK); client.UpdateLogic(TICK); accumulator -= TICK; }
   float alpha = accumulator / TICK;
   renderer.Draw(interpolate(prevState, currState, alpha));
   ```
3. **Interpolation auf Render-Seite:** Für jedes Objekt werden die Transforms der letzten zwei Ticks gespeichert, gerendert wird mit `alpha` dazwischen. Animationen (ABC-Keyframes) laufen ebenfalls interpoliert.
4. **Kamera und Mausblick laufen mit Render-Rate,** nicht mit Tick-Rate. Yaw und Pitch werden pro Frame direkt auf die Kamera angewandt und zum nächsten Tick an die Logik übergeben. Das ist der entscheidende Punkt für ein flüssiges Gefühl bei 144 Hz und mehr.
5. **HUD/ClientShell:** Bildschirm-Effekte, die pro Frame zählen, werden im Spielcode auf Zeitbasis umgestellt. Das sind gezielte, dokumentierte Patches, jeweils mit `#ifdef B2PORT_FIX` markiert.
6. **Verifikation:** Ein Deterministic-Replay-System zeichnet Inputs pro Tick auf. Testläufe vergleichen Spielerposition, Health und KI-Zustände mit Messungen aus dem Original bei 30 und 60 FPS. Ohne diese Messbasis lässt sich „1:1“ nicht belegen.

*Offene Frage für das Reverse Engineering:* Mit welcher effektiven Rate die Original-Engine auf 1998-Hardware lief und ob Teile der Physik schon damals intern in Teilschritten integrierten. Davon hängt ab, welcher feste Takt am originalgetreuesten ist.

### B.2 Hitscan- & Balance-Anpassungen der KI

Die KI-Gegner in Blood II (vor allem Kultisten mit Schusswaffen) gelten als sehr präzise Sofort-Treffer-Schützen. Die Anpassung muss **optional** sein, weil das Projektziel 1:1 lautet.

**Vorgehen:**
- **Profil-System:** `ai_profile classic | modern`, Default `classic` = Original-Werte, unverändert.
- Ansatzpunkte im Spielcode (KI-Feuerroutine und Waffen-Spread):
  1. **Reaktionsverzögerung:** 150–400 ms nach Sichtkontakt (abhängig vom Schwierigkeitsgrad), bevor der erste Schuss fällt.
  2. **Erster-Schuss-Fehlwurf / „Einschießen“:** Die Streuung startet hoch und konvergiert über 2–4 Schüsse.
  3. **Bewegungsabhängige Treffsicherheit:** Die Streuung wächst mit der Querbewegung des Spielers relativ zur KI. Dadurch wird Strafen wieder belohnt.
  4. **Distanzabfall** der Präzision statt nur des Schadens.
  5. **Schaden, Feuerrate und Gegnerzahl bleiben unverändert,** damit sich die Balance-Gesamtkurve nicht verschiebt.
- Alle Parameter liegen in einer Konfigurationsdatei statt hart im Code. So kann die Community nachjustieren.
- Validierung: Statistik pro Level (erlittener Schaden pro Minute) im Classic- und Modern-Profil vergleichen und auf eine moderate Absenkung zielen, z. B. 20–35 %.

### B.3 Formate: Parsing von REZ, DTX, ABC, DAT (v56)

| Format | Schwierigkeit | Quellen / Stand |
|---|---|---|
| **REZ** | niedrig | Einfaches Verzeichnis-Archiv (Header lokal verifiziert: „RezMgr Version 1“). Es gibt zahlreiche Extraktoren (z. B. Dragon UnPACKer) und den REZ-Manager-Code in der Jupiter-Linie als Referenz. Benötigt wird ein *virtuelles Dateisystem* mit Overlay-Reihenfolge. |
| **DTX** | niedrig–mittel | Textur-Header + Mipmaps; LT1 nutzt eine frühe DTX-Version mit 8-Bit-Paletten- und 16-Bit-Formaten. Spätere Versionen sind in Jupiter dokumentiert, die Unterschiede klein. |
| **ABC (Model)** | mittel | Frühe ABC-Version (Mesh, Node-Hierarchie, Vertex-Animation bzw. frühe Skelett-Keyframes, Sockets). Der Community-Blender-Importer für LithTech-ABC (ModDB-Tutorials) unterstützt mehrere Versionen. Ob das LT1-Format von Blood II abgedeckt ist, muss geprüft werden. |
| **DAT (World v56)** | **hoch** | Kritischster Punkt. Enthält BSP, Polygone, Surfaces, Lightmaps, Objekt-Liste mit Properties, Physik-Hülle und Sichtbarkeit. Öffentliche Dokumentation zu v56 ist dünn. Quellen: (a) Blood-II-SDK-Header und mitgelieferte Tools (DEdit erzeugt die Dateien, das liefert Strukturhinweise); (b) Jupiter-Loader für spätere Versionen als Strukturvorlage; (c) Reverse Engineering von `Server.dll`/`Client.exe` mit Ghidra; (d) Community-Notizen aus Blood-II- und Shogo-Modding (Shogo nutzt eine nahe Engine-Revision). |
| **Smacker (.smk)** | niedrig | FFmpeg-Decoder oder libsmacker |
| **DirectMusic (.sty/.sec/.dls/.per)** | **hoch** | siehe B.4 |

**Empfehlung:** Für DAT, ABC und DTX zuerst eine Formatspezifikation als Markdown/Kaitai-Struct (`.ksy`) schreiben, dann den Parser bauen. Kaitai liefert gleichzeitig einen Hex-Viewer zur Validierung und eine maschinenlesbare Doku für die Community.

### B.4 Musik: DirectMusic (unterschätzter Brocken)

Die Musik ist kein Streaming-Audio, sondern **interaktive DirectMusic-Komposition** (Styles, Segmente, Personalities, DLS-Instrumente). Unter Linux gibt es DirectMusic gar nicht, und unter Windows ist es veraltet.

Optionen:
1. **DirectMusic-Re-Implementierung einbinden:** Das OpenGothic-Umfeld (Gothic nutzt ebenfalls DirectMusic) hat eine Open-Source-Implementierung hervorgebracht (GothicKit „dmusic“). Eignung und Lizenz für Blood II müssen geprüft werden. Das ist der Weg mit dem höchsten Originaltreue-Potenzial. DLS-Instrumente werden dann z. B. über einen eigenen Sampler oder FluidSynth/TinySoundFont wiedergegeben.
2. **Fallback:** Die Original-Musik einmalig unter Windows rendern und als OGG-Stems abspielen. Dabei geht die Interaktivität teilweise verloren. Das wäre eine Konvertierung, die das Ziel „keine Vorab-Konvertierung durch den User“ verletzt, taugt also nur als Notlösung.

---

## C. Meilenstein-Plan

Aufwände sind grobe Schätzungen in Personenmonaten (PM) für erfahrene Entwickler.

| Phase | Inhalt | Ergebnis / Abnahmekriterium | PM |
|---|---|---|---|
| **0 – Recht & Recon** | Lizenztexte prüfen; SDK-Code beschaffen; Ghidra-Projekte für `Client.exe`, `Server.dll`, `d3d.ren`; Referenzaufnahmen (Video + Messwerte) im Original | Go/No-Go-Entscheidung; Liste aller vom Spielcode genutzten API-Funktionen | 1–2 |
| **1 – Asset-Toolkit** | VFS mit REZ-Stack; DTX-, ABC- und Smacker-Decoder; CLI-Extraktor + einfacher Viewer (Modelle + Animationen) | Alle Texturen und Modelle aus `BLOOD2*.REZ` fehlerfrei angezeigt; Testsuite gegen alle Dateien | 2–3 |
| **2 – Static Level Loader** | DAT-v56-Parser, BSP-Rendering mit Lightmaps, Objekt-Liste auslesen; freie Kamera; Renderer-Basis (OpenGL 4.5 oder Vulkan) | Alle Kampagnen- und Nightmare-Levels begehbar im Noclip-Modus, optisch vergleichbar mit dem Original | 3–5 |
| **3 – Spielcode-Build & API-Skelett** | SDK-Code auf modernen Compiler portieren (32 → 64 Bit, Win32-Abhängigkeiten kapseln); LT1-API als Stubs mit Logging | `Object` und `ClientShell` laden, Objekte werden erzeugt, Konsole läuft | 2–4 |
| **4 – Physik & Bewegung** | LT1-treue Kollision (BSP-Hülle, Boxen), `MoveObject`, Gravitation, Stufen, Wasser, Leitern; Fixed-Tick-Schleife + Interpolation; Raw-Input-Maus | Spieler bewegt sich messbar wie im Original (Sprungweite, Geschwindigkeit, Stufen); stabil bei 30–360 FPS | 3–5 |
| **5 – Gameplay-Vollständigkeit** | Waffen, Projektile, Partikel/Sprites, Trigger, Türen/Mover, KI-Navigation, Sound über OpenAL Soft, Savegames, HUD/Menüs auflösungsunabhängig | Kampagne von Anfang bis Ende durchspielbar | 4–8 |
| **6 – Musik & Video** | DirectMusic-Ersatz, Smacker-Cutscenes | Dynamische Musik reagiert wie im Original | 1–3 |
| **7 – Modernisierung** | Widescreen/Ultrawide (Hor+-FOV), FOV-Regler, Gamepad (SDL GameController), Remapping-UI, AI-Profil „modern“, HRTF/Surround | Steam Deck spielbar ohne Proton; alle Hauptanforderungen erfüllt | 2–4 |
| **8 – Plattform & Release** | Linux-Builds (AppImage/Flatpak), Steam-Deck-Profil, automatischer Asset-Fund (GOG/Steam-Pfade), CI (GitHub Actions), Nightmare Levels finalisieren | Release 1.0 | 1–2 |
| **Optional 9 – Multiplayer** | LT1-Netzprotokoll nachbauen oder eigenes Protokoll | MP-Kompatibilität untereinander (nicht mit dem Original) | 3–6 |

**Kritischer Pfad:** Phase 2 (DAT v56) → Phase 4 (Physik). Wenn das DAT-Format nach Phase 2 nicht vollständig verstanden ist, verschiebt sich alles Weitere.

---

## D. Tools & Bibliotheken

| Bereich | Empfehlung | Anmerkung |
|---|---|---|
| Plattform, Fenster, Input, Gamepad | **SDL2** (stabil) oder **SDL3** | Raw-Mausinput (`SDL_SetRelativeMouseMode`), GameController-DB, Hotplug |
| Rendering | **OpenGL 4.5 Core** zum Start, später **Vulkan**; optional abstrahiert über **bgfx** oder **Diligent Engine** | Kleine Geometriemengen; ein schlanker GL-Renderer genügt für V1 |
| Shader | GLSL → SPIR-V via **glslang/shaderc** | |
| Mathe | **glm** | LT1 nutzt eigene Typen (`DVector`, `DRotation`); dünne Adapter, keine Neuschreibung im Spielcode |
| Audio | **OpenAL Soft** | HRTF, 5.1/7.1, EFX als EAX-Ersatz |
| Audio-Decoding | **dr_wav / dr_libs**, **stb_vorbis** | Original-Sounds sind überwiegend WAV/ADPCM |
| Musik | **GothicKit dmusic** (prüfen), **TinySoundFont** oder **FluidSynth** für DLS | siehe B.4 |
| Video | **FFmpeg (libavcodec)** oder **libsmacker** | Smacker-Decoder vorhanden |
| UI / Debug | **Dear ImGui** | Konsole, Level-Inspector, Physik-Debugger |
| Formatanalyse | **Ghidra** (+ ggf. IDA Free), **ImHex**, **Kaitai Struct** | Spezifikationen als `.ksy` versionieren |
| Asset-Export (Tools) | **assimp** oder **cgltf** (glTF-Export) | Nur für Viewer/Export, **nicht** zur Laufzeit. Die Laufzeit liest ABC direkt. |
| Build | **CMake** + **vcpkg** oder **Conan**; Clang/MSVC/GCC | |
| Tests | **Catch2** / **doctest**, Replay-Tests für Physik-Determinismus | |
| Profiling | **Tracy** | Frametime-Analyse bei 144 Hz und mehr |
| Referenzen (nur lesen) | [`jsj2008/lithtech`](https://github.com/jsj2008/lithtech) (Jupiter EX, GPL), Blood-II-SDK, Shogo-Quellcode | Clean-Room bei GPL-Code beachten |

---

## E. Risiken (Übersicht)

| Risiko | Wahrscheinlichkeit | Auswirkung | Gegenmaßnahme |
|---|---|---|---|
| SDK-Lizenz erlaubt keine Verteilung | mittel | **Projekt-Stopp** oder Nutzer muss Spielcode selbst bauen | Phase 0, Juristen/Rechteinhaber kontaktieren |
| DAT v56 nicht vollständig dekodierbar | mittel | hoch | Früh in Phase 2 investieren, DEdit-Ausgaben vergleichen |
| Physik nicht 1:1 reproduzierbar | mittel | hoch | Messbasis aus dem Original, Replay-Tests |
| DirectMusic-Ersatz unvollständig | mittel | mittel | Fallback auf gerenderte Stems |
| Spielcode voller 32-Bit- und Win32-Annahmen | hoch | mittel | Zuerst 32-Bit-Build, 64 Bit als zweiter Schritt |
| Personalmangel / Motivation | hoch | hoch | Früh sichtbare Meilensteine (Viewer, Level-Walk) für die Community |

---

## Quellen

- [jsj2008/lithtech – Jupiter-EX-basierter LithTech-Port (GitHub)](https://github.com/jsj2008/lithtech)
- [rohit-n/lithtech – Fork/Mirror](https://github.com/rohit-n/lithtech)
- [ModDB – LithTech Engine Downloads (u. a. Blood-2-Quellcode, Compile-Fixes)](https://www.moddb.com/engines/lithtech/downloads)
- [Blood Wiki – LithTech](https://blood-wiki.org/index.php/LithTech)
- [Wikipedia – Blood II: The Chosen](https://en.wikipedia.org/wiki/Blood_II:_The_Chosen)
- [ModDB-Tutorial – LithTech-ABC-Modelle in Blender](https://www.moddb.com/tutorials/replacing-weapon-models3)

*Nicht verifiziert (nach Kenntnisstand, vor Nutzung prüfen):* genaue Versionsnummern von DTX und ABC in LT1, Abdeckung des Blender-ABC-Importers für Blood II, Lizenz und Eignung von GothicKit dmusic.
