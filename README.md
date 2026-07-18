# srs — Speedrun Sheet

[Everest](https://everestapi.github.io/) mod, **companion** to [SpeedrunTool](https://gamebanana.com/mods/53712) for Celeste: it imports the community practice sheet (Google Sheets → local CSV) and, when SpeedrunTool's room timer completes, shows the tier you reached (Gold, Pink, Purple, … Unranked) in its color under the timer.

Options under **Mod Options → Speedrun Sheet**.

## Installation

1. Install [Everest](https://everestapi.github.io/) and [SpeedrunTool](https://gamebanana.com/mods/53712) (v3.27.16+), e.g. via Olympus.
2. Download/build srs (see [Build](#build)) and put `srs.zip` (or a `srs` folder containing `srs.dll`, `everest.yaml`, `Dialog/`) into `<Celeste>/Mods/`.
3. Make sure `SpeedrunTool.zip` is **not** in `Mods/blacklist.txt` — it's a dependency.

## Usage

- **Mod Options → Speedrun Sheet → Update Sheet Data**: downloads the practice sheet as CSV (no Google account involved — the sheet just needs to be shared "anyone with the link"). The data is cached in `Saves/srs/sheet.csv` and reloaded on startup, so everything keeps working offline. Nothing is ever downloaded automatically.
- **Manual import**: export the sheet yourself (File → Download → CSV) and drop it at `Saves/srs/sheet.csv` — same effect as the button.
- The sheet URL can be changed in `Saves/modsettings-srs.celeste` (`SheetUrl`).

## Build

```
dotnet build -p:CelestePrefix=<Celeste folder>
```

`CelestePrefix` is auto-detected if the repo is cloned into `<Celeste>/Mods/xxx/`. SpeedrunTool's DLL is automatically extracted from the installed `SpeedrunTool.zip`. The `OutputAsModStructure` target generates `build/`, ready to zip as `srs.zip` (or copy as a `srs` folder) into `<Celeste>/Mods/`.

## Changelog

### v1.0.0 — 2026-07-18

- **Checkpoint auto-detection** (phase 4bis): a new **Auto-Detect Checkpoint** toggle (on by default) makes the checkpoint you are playing drive the selection — the chapter comes from the current session (including the 5A/5B and 6A/6B route sides), the checkpoint is the last checkpoint room entered (or the one picked on the chapter panel), updated on every room transition and registered with SpeedrunTool's save states: loading a savestate re-selects the checkpoint of the moment of the save. The **Chapter**/**Checkpoint** sliders are greyed out while auto-detection is on and act as a manual override when it is off. Detection pauses while a completed run's tier is on screen (finishing a run walks you into the next checkpoint's room — switching to it would discard the result) and resumes when the timer resets (savestate load, timer clear). Game↔sheet name differences ("500 M" → "500m", 6B "Reflection" → "Falling", …) are mapped by a hardcoded table — the current sheet is a prototype, so no name normalization on purpose. "Hollows Tape" cannot be auto-detected (it starts at 6A's Hollows checkpoint): select it manually.
- **Tier display hotkey** (phase 5): rebindable key/button (unbound by default, in Everest's key/button config for srs) toggling the tier row in-game, confirmed with SpeedrunTool's on/off popup.

### v0.3.0 — 2026-07-18

- **Tier comparison & display** (phase 4): when SpeedrunTool's room timer completes, the final time is compared against the selected checkpoint's sheet thresholds (first threshold ≥ your time wins; slower than Red 3 is Unranked) and the tier name is drawn in its color in a row under the timer — below [srta](https://github.com/avonfrieren/srta)'s delta row when srta is installed. Tier colors come from the sheet's own column names (all XNA named colors); the row follows save states like the timer itself, and a **Show Tier** toggle was added to Mod Options.
- **Full-run detection by room count**: selecting a checkpoint automatically sets SpeedrunTool's **Number of Rooms** to that checkpoint's room count, so a run started from the beginning of the checkpoint makes the timer stop exactly on its last room — that completion is what gets compared to the sheet. The tier only shows when Number of Rooms still matches the selected checkpoint (changing it by hand means partial-segment practice, so no tier). Final checkpoints of each chapter (unknown room counts) get Number of Rooms 99, so their runs end through chapter completion (or the cassette/summit flags). The room timer (Next Room / Current Room) still has to be enabled in SpeedrunTool for anything to show.

### v0.2.0 — 2026-07-18

- **Checkpoint selection** (phase 3): the imported tab is now "Celeste Any% Standards CP's" — every checkpoint of the any% route with its own tier times (e.g. 1a → Start, Crossing, Chasm), covering both route choices (5a/b and 6a/b). Two new entries in Mod Options — **Chapter** and **Checkpoint** — pick the checkpoint to compare against (persisted; picking a chapter rebuilds the checkpoint list). Existing installs with the old default `SheetUrl` (IL tab) are migrated automatically; press **Update Sheet Data** once to fetch the checkpoint data. Old chapter-only CSVs still parse.
- **Checkpoint list cleanup**: chapter echoes are dropped from checkpoint names ("1a Start" → "Start"; side prefixes like "5a Start"/"5b Start" are kept — they disambiguate the two routes), the sheet's "Wake Up" rows are skipped (they time the wake-up animation, not a checkpoint), and the "6a"/"6a Route"/"6b Route" groups are folded into a single **6a/b** chapter like the sheet's own 5a/b, with the duplicated "Rock Bottom" shown as "6a Rock Bottom"/"6b Rock Bottom".

### v0.1.0 — 2026-07-18

- **Mod skeleton**: Everest module with SpeedrunTool declared as a dependency (same companion approach as [srta](https://github.com/avonfrieren/srta)).
- **Sheet import**: downloads the "Celeste Any% Standars CP'S" tab of the practice sheet as CSV on demand, parses the tier columns (Gold → Red 3, Unranked) and the segment rows (Prologue, 1a … 7a, 6a Tape + Chapter Times block) into typed data, and caches the CSV locally with a status line (segment count + date) in Mod Options. Handles mixed time formats (`28`, `28.1`, `1:05.5`, `24:06.802`), offline play, private-sheet detection, and manual CSV drop-in as a fallback.
