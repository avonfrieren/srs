# Speedrun Sheet (srs)

[Everest](https://everestapi.github.io/) mod for Celeste that requires [Speedrun Tool](https://gamebanana.com/tools/6597). It imports a community practice sheet's reference times, compares your time on a checkpoint with them, and shows the tier you reached in its color. It detects the segment you run on its own: there is nothing to pick.

Options under **Mod Options → Speedrun Sheet**. What changed in each version is in [CHANGELOG.md](CHANGELOG.md).

## Getting started

**Requirements.** Everest 1.6397 or later, and Speedrun Tool 3.27.17 or later. srs times segments with the chapter time, so records and the export do not depend on the room timer's type.

**What you see.** Under Speedrun Tool's room timer, srs adds one row: your time on the last segment you completed, in the color of the tier it reached (`Gold`, `Purple 2`, ...), or `Unranked` past the last tier. When several segments complete together, it shows the most specific one. The row is drawn under that timer, so it shows only while the timer is on. It can be turned off in Mod Options (**Show Tier**).

**Detection.** There is nothing to select. srs times every segment that can be running, so a chain of checkpoints records each segment of the chain.

- A segment is recorded when you walk into its first room from the room before it, however you reached that room: a chain, a savestate, the debug map, `console load` or Speedrun Tool's room teleport. Walking in from anywhere else records nothing.
- A chapter's first segment, and the segment after a wake-up, also start from a savestate made where you appeared in that room, if you had not moved since. A tap of a single frame is a move. Entering a chapter starts its first segment only on the chapter's own spawn.
- A savestate load or a room teleport starts a new attempt: the segment you were in is not recorded.
- The `Heart`, `Tape` and `DTS` rows are told apart by what you collected, and in Farewell by the dashes you have left. 2A's `Start Heart RC` runs until Restart Chapter.
- A segment of an hour or more is not recorded.

**Hotkeys.** Two, both unbound by default: Toggle Tier Display and Open Sheet Export. Bind them from **Keybinds**, at the bottom of srs's section. A hotkey can be a combo: all of its keys must be held together.

**Exporting your times (optional).** srs can write the segments you ran into your own copy of the practice sheet. It needs a script deployed on that copy, which takes a few minutes once: see [AppsScript/SETUP.md](AppsScript/SETUP.md). The export screen shows the segment whose best improved last. Your bests are kept until you enter another chapter, so a chapter's last segment can be exported by entering the chapter again.

**The reference times.** srs downloads them at launch, or when you press **Update Standards**, and keeps a copy in `Saves/srs/` (`asides.csv`, `bsides.csv`, `farewell.csv`), so it works offline. CSVs dropped there by hand are read as they are.

**When something looks wrong**, look in Celeste's `log.txt` for lines tagged `[srs]`.

**Known limits.**

- Speedrun Tool's confetti and its PB display no longer trigger, because srs decides where a run ends itself.
- Only part of the sheet is imported: individual-level rows, C-sides, and the B-side rows off the Any% route are not yet.
- srs trusts that your progress in the chapter (opened doors, used keys, broken blocks) is what a real run would have at that point.
