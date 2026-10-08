# Speedrun Sheet (srs)

[Everest](https://everestapi.github.io/) mod for Celeste that requires [Speedrun Tool](https://gamebanana.com/tools/6597). It imports a community practice sheet's reference times, compares your time on a checkpoint with them, and shows the tier you reached in its color. It detects the segment you run on its own: there is nothing to pick.

Options under **Mod Options → Speedrun Sheet**. What changed in each version is in [CHANGELOG.md](CHANGELOG.md).

## Getting started

**Requirements.** Everest 1.6397 or later, and Speedrun Tool 3.27.17 or later. srs times segments with the chapter time, so records and the export do not depend on the room timer's type.

**What you see.** When you complete a segment, srs adds two rows above Speedrun Tool's room timer. On top, the segment it detected (`1a Crossing`). Below it, your time and the tier it reached, in that tier's color (`Gold`, `Purple 2`, ...), or `Unranked` past the last tier; then `PB` and what you gained (`PB -0.214`), in gold, when your own sheet is set up for export and holds a slower time; then how far you are from the next tier (`+0.140 to Purple 1`) in red, or your margin under the WR (`-0.120 to WR`) in green. A tier needs a time strictly under its threshold, except WR, which a tie reaches, as on the sheet. When several segments complete together, it shows the most specific one. The rows stay until you leave the room or load a savestate, and show only while the timer is on. Mod Options hide each part: **Show Checkpoint Name**, **Show Time**, **Show Tier** (the tier's name; the time keeps its color), **Show PB Improvement** and **Show Delta to Next Tier**, all on by default.

**Detection.** There is nothing to select. srs times every segment that can be running, so a chain of checkpoints records each segment of the chain.

- A segment is recorded when you walk into its first room from the room before it, however you reached that room: a chain, a savestate, the debug map, `console load` or Speedrun Tool's room teleport. Walking in from anywhere else records nothing.
- A chapter's first segment, and the segment after a wake-up, also start from a savestate made where you appeared in that room, if you had not moved since. A tap of a single frame is a move. Entering a chapter starts its first segment only on the chapter's own spawn.
- A savestate load or a room teleport starts a new attempt: the segment you were in is not recorded.
- The `Heart`, `Tape` and `DTS` rows are told apart by what you collected, and in Farewell by the dashes you have left. 2A's `Start Heart RC` runs until Restart Chapter.
- A segment of an hour or more is not recorded.

**Hotkeys.** Two, both unbound by default: Toggle Tier Display and Open Sheet Export. Bind them from **Keybinds**, at the bottom of srs's section. A hotkey can be a combo: all of its keys must be held together.

**Exporting your times (optional).** srs can write the segments you ran into your own copy of the practice sheet. It needs a script deployed on that copy, which takes a few minutes once: see [AppsScript/SETUP.md](AppsScript/SETUP.md). The export screen lists every segment you ran this session, in every chapter, next to the time your sheet holds; the ones you improved start ticked, and one export sends every row you tick. srs keeps a copy of your sheet's times, so the screen and the PB on the timer work before your sheet answers and offline, and the screen says how old that copy is.

**The reference times.** srs downloads them at launch, or when you press **Update Standards**, and keeps a copy in `Saves/srs/` (`asides.csv`, `bsides.csv`, `farewell.csv`), so it works offline. CSVs dropped there by hand are read as they are.

**When something looks wrong**, look in Celeste's `log.txt` for lines tagged `[srs]`.

**Known limits.**

- Speedrun Tool's confetti and its PB display no longer trigger, because srs decides where a run ends itself.
- Only part of the sheet is imported: individual-level rows, C-sides, and the B-side rows off the Any% route are not yet.
- srs trusts that your progress in the chapter (opened doors, used keys, broken blocks) is what a real run would have at that point.
