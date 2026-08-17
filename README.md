# Speedrun Sheet (srs) 

[Everest](https://everestapi.github.io/) mod that **require** [Speedrun Tool](https://gamebanana.com/tools/6597) for Celeste. It works by importing a community practice sheet's time references and compare your time on a checkpoint with it to find and show the associated color. It also auto detect the checkpoint your in.

Options under **Mod Options → Speedrun Sheet**.

## Changelog

### v3.5.0 — 2026-08-17

- **The sheet updates itself on launch**: srs now refreshes the three tabs in the background every time the game starts, so a retimed or extended sheet reaches you without a trip to Mod Options. The cached data loads first and keeps being used while the download runs, and a failed download changes nothing — offline, you simply keep the last cache. **Update Sheet Data** stays for refreshing mid-session; pressing it while the launch refresh is still running joins that one instead of downloading everything twice. A first install with no cache at all now fills itself in, too.

### v3.4.0 — 2026-08-17

- **Core, Farewell and the True Ending categories**: the run past 7A is now covered. **8a** brings `Start`, `Into the Core`, `Hot and Cold`, `HotM Vertical` and `HotM Horizontal`; the new **Farewell** chapter brings `Start`, `Singular`, `Power Source`, `Remembered`, `Event Horizon`, `Determination`, `Stubborness`, `Reconciliation` and `Farewell`. Two new categories go with them — **True Ending**, which adds the 3A and 4A hearts (`Huge Mess Heart`, `Shrine Heart`: the same segments as their plain siblings, run with the heart detour in them) to the any% segments, and **True Ending DTS**, the same run with the double-dash skip: on it, Farewell's first six checkpoints select their `... DTS` row instead. Everything else keeps falling back to its any% row, so the two new categories are complete runs, not partial ones.
- **A third tab is imported**: `Farewell Standards`, cached as `Saves/srs/farewell.csv` next to the other two. Press **Update Sheet Data** once after updating — the Farewell chapter stays empty until you do, the rest keeps working meanwhile. The tab has no chapter column and stops one tier column short of the others; both are handled at import.
- **Only `RTM` rows stop a run early**: a `Clear` suffix on a checkpoint row does not mean the chapter's completion — it means the run collects the item and keeps going, as opposed to the `RTM` row next to it. `Shrine Heart` therefore ends at Old Trail like plain `Shrine` does, not two checkpoints later. The sheet's own numbers say so: 27.5s cannot contain the 78s of run that follow, and the chapter totals go up by exactly what the detour costs that one segment.
- **HotM is split where the sheet splits it**: the game gives 8A's finale a single *Heart of the Mountain* checkpoint, the sheet times its vertical climb and its horizontal chase separately. `HotM Vertical` now ends — and `HotM Horizontal` starts — on entering room `d-08`, where the climb tops out. Auto-detection moves there too, without waiting for a checkpoint the game does not have.

### v3.3.0 — 2026-08-16

- **Selection row**: a discreet greyed row under the tier row now names the comparison that is armed, as `category - checkpoint` (`Any% - Chasm`, `Any% Cassettes - Hollows Tape`) — the checkpoint auto-detection picked, or the sliders' if it is off. New **Show Selection** toggle in Mod Options (on by default) plus its own rebindable hotkey (unbound by default), like the tier row's. It shows as soon as the room timer does and keeps its place: the tier row's slot above it stays empty until a run finishes, so a completion never slides the row you were reading. Nothing is drawn while no segment is selected (no sheet data yet, or a selection the imported data no longer has).

### v3.2.0 — 2026-08-06

- **Segments the sheet does not time from their checkpoint**: two any% segments start somewhere else than the in-game checkpoint they belong to, and both were mistimed. **2A Awake** is timed from the moment Madeline wakes up — room `end_0`, the campfire right after the dream section — which is *three* rooms before the game's Awake checkpoint; it now starts there, and a run of **Intervention** ends there too instead of overrunning into Awake's first three rooms. **7A 0m** is timed from `a-00`: neither the intro room nor Madeline's landing animation counts, their time being added on the sheet afterwards (run it the usual way — savestate placed after the landing, **Current Room** timer). Auto-detection follows: entering one of those rooms selects the segment, without waiting for the checkpoint.
- **0m gets its untimed head added back**: the 5.508s of the intro room and the landing animation are part of what the sheet's 0m thresholds describe, so srs adds them to the captured time. Without it a 0m run was compared against thresholds covering more than it had timed, and came out several tiers too high. The greyed, tierless time of a run that did not start at `a-00` is left exactly as the timer showed it.
- **The captured time was one frame too long**: srs read the room timer *after* the frame it ends on had been added to it, while SpeedrunTool freezes its own display just before. Every result was one frame slower than the timer above it — and than the sheet's references, which are recorded with SpeedrunTool. srs now reads the same value SpeedrunTool freezes.
- **"Cassette" is now "Any% Cassettes"**: the 5A and 6A cassettes are part of the any% run, so the category says which run its segments belong to. The rule behind the naming: a category never holds two segments starting at the same in-game checkpoint, and wider categories are described by what they add to a smaller one.

### v3.1.0 — 2026-08-06

- **Switch Category hotkey**: a new rebindable key/button (unbound by default, in Everest's key/button config for srs) cycles the **Category** setting without leaving the level, confirmed with SpeedrunTool's on/off popup. Auto-detection re-resolves the checkpoint on the same frame, so pressing it on 6A's Hollows switches the comparison between **Hollows** and **Hollows Tape** live.
- **Shorter tier row background**: the row's black background now uses SpeedrunTool's own width rule instead of the measured text width, so it ends at the same place its rows do. It used to push the whole fade past the last character, making the srs row visibly longer than the time and PB rows above it.

### v3.0.0 — 2026-08-06

- **Category selector**: a new **Category** slider in Mod Options (**Any%** / **Cassette**, persisted) tells the mod which practice category you are running, and auto-detection uses it to resolve the checkpoints that exist in several sheet variants. On **Cassette**, reaching 5A's Depths checkpoint selects **Depths Tape** and 6A's Hollows selects **Hollows Tape** — the two segments that previously could only be picked by hand with auto-detection turned off. Checkpoints without a variant in the active category keep selecting their plain any% row.
- **Declarative end conditions replace room counts**: a run of the selected segment now ends when its own condition fires — entering the next in-game checkpoint's room (resolved from the game's data at runtime, no hand-entered counts anymore), completing the chapter (chapter finals, Granny), or **collecting the cassette** for the two Tape segments (the community convention for 📼 RTM segments; previously they ended one room early, on entering the cassette room). The hand-maintained room count table is gone, and with it its route fragility — a checkpoint's segment now ends at the right place whatever path you take through it.
- **SpeedrunTool's Number of Rooms is yours again**: srs no longer overwrites the setting on every selection change. SpeedrunTool's timer display follows it independently; srs evaluates its own end conditions and shows its own result.
- **The final time is displayed**: the srs row now shows the captured run time to the left of the tier name, both in the tier's color — it is the reference time of the run, frozen at the segment's real end.
- **Start guard reworked**: the tier only shows when the run actually started at the selected segment's first room — the room the timing started from (savestate-aware, and dependent on the Next Room / Current Room timer type). A run that ends without having started there still freezes its time on the row, greyed and tierless, like the old partial-practice rule but visible.

### v2.0.0 — 2026-08-05

- **New practice sheet**: the mod now imports the community's current time-reference sheet (the old prototype sheet is retired). Data comes from two tabs — one for every A-side checkpoint, one for the any% route's 5B/6B checkpoints — downloaded and cached separately (`Saves/srs/asides.csv` + `bsides.csv`; the old `sheet.csv` cache is cleaned up automatically). Press **Update Sheet Data** once after installing to fetch the new data.
- **Same checkpoints as before, plus Depths Tape**: only the checkpoints the mod already supported are imported (the sheet's heart/cassette/gem emoji variants and the per-chapter IL rows are for later), still folded into the familiar `5a/b` / `6a/b` chapters. The two cassette rows `Depths 📼 RTM` and `Hollows 📼 RTM` are imported as **Depths Tape** (new — 8 rooms, ending in the cassette room, manual-only like Hollows Tape) and **Hollows Tape**.
- **7A "Start" is now "0m"**, matching the new sheet's naming; auto-detection and the room count follow.

### v1.1.0 — 2026-07-20

- **Distinct tier colors**: each tier now draws in its own palette color instead of an XNA named color. The rank suffix is significant, so `Purple 1`/`Purple 2`/`Purple 3` (and every other ranked tier) are three separate shades rather than one — matching the sheet's own coloring. WR and Hidden stay white, Unranked stays grey.
- **Hollows Tape room count**: the 6B cassette-route "Hollows Tape" checkpoint now completes on a 2-room count instead of never completing (grabbing the 6A cassette doesn't stop SpeedrunTool's room timer), so it gets a tier. It still has to be selected manually.

### v1.0.0 — 2026-07-18

- **Checkpoint auto-detection** (phase 4bis): a new **Auto-Detect Checkpoint** toggle (on by default) makes the checkpoint you are playing drive the selection — the chapter comes from the current session (including the 5A/5B and 6A/6B route sides), the checkpoint is the last checkpoint room entered (or the one picked on the chapter panel), updated on every room transition and registered with SpeedrunTool's save states: loading a savestate re-selects the checkpoint of the moment of the save. The **Chapter**/**Checkpoint** sliders are greyed out while auto-detection is on and act as a manual override when it is off. Detection pauses while a completed run's tier is on screen (finishing a run walks you into the next checkpoint's room — switching to it would discard the result) and resumes when the timer resets (savestate load, timer clear). Game↔sheet name differences ("500 M" → "500m", 6B "Reflection" → "Falling", …) are mapped by a hardcoded table — the current sheet is a prototype, so no name normalization on purpose. "Hollows Tape" cannot be auto-detected (it starts at 6A's Hollows checkpoint): select it manually.
- **Tier display hotkey** (phase 5): rebindable key/button (unbound by default, in Everest's key/button config for srs) toggling the tier row in-game, confirmed with SpeedrunTool's on/off popup.

### v0.3.0 — 2026-07-18

- **Tier comparison & display** (phase 4): when SpeedrunTool's room timer completes, the final time is compared against the selected checkpoint's sheet thresholds (first threshold ≥ your time wins; slower than Red 3 is Unranked) and the tier name is drawn in its color in a row under the timer — below [srta](https://github.com/avonfrieren/srta)'s delta row when srta is installed. Tier colors come from the sheet's own column names (all XNA named colors); the row follows save states like the timer itself, and a **Show Tier** toggle was added to Mod Options.
- **Full-run detection by room count**: selecting a checkpoint automatically sets SpeedrunTool's **Number of Rooms** to that checkpoint's room count, so a run started from the beginning of the checkpoint makes the timer stop exactly on its last room — that completion is what gets compared to the sheet. The tier only shows when Number of Rooms still matches the selected checkpoint (changing it by hand means partial-segment practice, so no tier). Final checkpoints of each chapter (unknown room counts) get Number of Rooms 99, so their runs end through chapter completion (or the cassette/summit flags). The room timer (Next Room / Current Room) still has to be enabled in SpeedrunTool for anything to show.

### v0.2.0 — 2026-07-18

- **Checkpoint selection** (phase 3): the imported tab is now the any% checkpoints one — every checkpoint of the any% route with its own tier times (e.g. 1a → Start, Crossing, Chasm), covering both route choices (5a/b and 6a/b). Two new entries in Mod Options — **Chapter** and **Checkpoint** — pick the checkpoint to compare against (persisted; picking a chapter rebuilds the checkpoint list). Existing installs with the old default `SheetUrl` (IL tab) are migrated automatically; press **Update Sheet Data** once to fetch the checkpoint data. Old chapter-only CSVs still parse.
- **Checkpoint list cleanup**: chapter echoes are dropped from checkpoint names ("1a Start" → "Start"; side prefixes like "5a Start"/"5b Start" are kept — they disambiguate the two routes), the sheet's "Wake Up" rows are skipped (they time the wake-up animation, not a checkpoint), and the "6a"/"6a Route"/"6b Route" groups are folded into a single **6a/b** chapter like the sheet's own 5a/b, with the duplicated "Rock Bottom" shown as "6a Rock Bottom"/"6b Rock Bottom".

### v0.1.0 — 2026-07-18

- **Mod skeleton**: Everest module with SpeedrunTool declared as a dependency.
- **Sheet import**: downloads the practice sheet's any% checkpoints tab as CSV on demand, parses the tier columns (Gold → Red 3, Unranked) and the segment rows (Prologue, 1a … 7a, 6a Tape + Chapter Times block) into typed data, and caches the CSV locally with a status line (segment count + date) in Mod Options. Handles mixed time formats (`28`, `28.1`, `1:05.5`, `24:06.802`), offline play, private-sheet detection, and manual CSV drop-in as a fallback.
