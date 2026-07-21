# Speedrun Sheet (srs) 

[Everest](https://everestapi.github.io/) mod that **require** [Speedrun Tool](https://gamebanana.com/tools/6597) for Celeste. It works by importing Astro's practice sheet and compare your time on a checkpoint with it to find and show the associated color. It also auto detect the checkpoint your in.

Options under **Mod Options → Speedrun Sheet**.

## Notes

- **Checkpoints that start at the same place as another one are not auto-detected.** A few sheet segments begin at the exact same in-game checkpoint as another segment, so auto-detection can't tell them apart and always picks the other one. Right now this is only **Hollows Tape**, which starts at 6A's **Hollows** checkpoint (so it gets detected as *Hollows*). To practice one of these, turn **Auto-Detect Checkpoint** off and select it by hand with the **Chapter** / **Checkpoint** sliders — the run then completes on its own room count and the tier shows.

## Changelog

### v1.1.0 — 2026-07-20

- **Distinct tier colors**: each tier now draws in its own palette color instead of an XNA named color. The rank suffix is significant, so `Purple 1`/`Purple 2`/`Purple 3` (and every other ranked tier) are three separate shades rather than one — matching the sheet's own coloring. WR and Hidden stay white, Unranked stays grey.
- **Hollows Tape room count**: the 6B cassette-route "Hollows Tape" checkpoint now completes on a 2-room count instead of never completing (grabbing the 6A cassette doesn't stop SpeedrunTool's room timer), so it gets a tier. It still has to be selected manually — see Notes.

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
