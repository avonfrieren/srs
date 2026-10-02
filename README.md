# Speedrun Sheet (srs)

[Everest](https://everestapi.github.io/) mod for Celeste that requires [Speedrun Tool](https://gamebanana.com/tools/6597). It imports a community practice sheet's reference times, compares your time on a checkpoint with them, and shows the tier you reached in its color. It also auto-detects the checkpoint you're in.

Options under **Mod Options → Speedrun Sheet**. What changed in each version is in [CHANGELOG.md](CHANGELOG.md).

## Getting started

**Requirements.** Everest 1.6397 or later, and Speedrun Tool 3.27.17 or later with its **room timer on**, set to *Next Room* or *Current Room*. srs reads the room timer to time your runs; with it off, nothing is ever timed.

**What you see.** Under Speedrun Tool's timer, srs adds two rows:

- When you finish a segment, your time in the color of the tier it reached (`Gold`, `Purple 2`, ...), or `Unranked` past the last tier. A time shown in grey means the run did not start at the segment's first room, so it earns no tier.
- A greyed row naming the segment the next run will be compared against, as `category - checkpoint`, for example `Any% - Chasm`.

Both rows can be turned off in Mod Options (**Show Tier**, **Show Selection**).

**Picking the segment.** With **Auto-Detect Checkpoint** on, the default, the checkpoint you're playing selects the segment. **Category** (Any%, Any% Cassettes, True Ending, True Ending DTS) decides which variant a checkpoint maps to when the sheet has several, for example `Hollows` or `Hollows Tape`. With auto-detection off, the Chapter and Checkpoint sliders choose instead.

**Hotkeys.** Four, all unbound by default: Switch Category, Toggle Tier Display, Toggle Selection Display and Open Sheet Export. Bind them from **Keybinds**, at the bottom of srs's section. A hotkey can be a combo: all of its keys must be held together.

**Exporting your times (optional).** srs can write the segment you just ran into your own copy of the practice sheet. It needs a script deployed on that copy, which takes a few minutes once: see [AppsScript/SETUP.md](AppsScript/SETUP.md).

**The reference times.** srs downloads them at launch, or when you press **Update Standards**, and keeps a copy in `Saves/srs/` (`asides.csv`, `bsides.csv`, `farewell.csv`), so it works offline. CSVs dropped there by hand are read as they are.

**When something looks wrong**, look in Celeste's `log.txt` for lines tagged `[srs]`.

**Known limits.**

- Speedrun Tool's confetti and its PB display no longer trigger, because srs decides where a run ends itself.
- Only part of the sheet is imported: individual-level rows, C-sides, and the B-side rows off the Any% route are not yet.
