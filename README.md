# srs — Speedrun Sheet

[Everest](https://everestapi.github.io/) mod, **companion** to [SpeedrunTool](https://gamebanana.com/mods/53712) for Celeste: it imports the community practice sheet (Google Sheets → local CSV) and — in upcoming versions — will color a segment's final room-timer time according to the sheet's tier thresholds (Gold, Pink, Purple, … Unranked).

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

### v0.2.0 — 2026-07-18

- **Checkpoint selection** (phase 3): the imported tab is now "Celeste Any% Standards CP's" — every checkpoint of the any% route with its own tier times (e.g. 1a → Start, Crossing, Chasm), covering both route choices (5a/b and 6a/b). Two new entries in Mod Options — **Chapter** and **Checkpoint** — pick the checkpoint to compare against (persisted; picking a chapter rebuilds the checkpoint list). Existing installs with the old default `SheetUrl` (IL tab) are migrated automatically; press **Update Sheet Data** once to fetch the checkpoint data. Old chapter-only CSVs still parse.
- **Checkpoint list cleanup**: chapter echoes are dropped from checkpoint names ("1a Start" → "Start"; side prefixes like "5a Start"/"5b Start" are kept — they disambiguate the two routes), the sheet's "Wake Up" rows are skipped (they time the wake-up animation, not a checkpoint), and the "6a"/"6a Route"/"6b Route" groups are folded into a single **6a/b** chapter like the sheet's own 5a/b, with the duplicated "Rock Bottom" shown as "6a Rock Bottom"/"6b Rock Bottom".

### v0.1.0 — 2026-07-18

- **Mod skeleton**: Everest module with SpeedrunTool declared as a dependency (same companion approach as [srta](https://github.com/avonfrieren/srta)).
- **Sheet import**: downloads the "Celeste Any% Standars CP'S" tab of the practice sheet as CSV on demand, parses the tier columns (Gold → Red 3, Unranked) and the segment rows (Prologue, 1a … 7a, 6a Tape + Chapter Times block) into typed data, and caches the CSV locally with a status line (segment count + date) in Mod Options. Handles mixed time formats (`28`, `28.1`, `1:05.5`, `24:06.802`), offline play, private-sheet detection, and manual CSV drop-in as a fallback.
