using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.SpeedrunSheet;

// the category a sheet row belongs to, read from the marker in its raw name
// ("Hollows 📼 RTM" is the cassette variant of "Hollows"; no marker = plain
// any% standard). A row can be practiced in several categories — the heart
// rows belong to both True Ending variants — so this is only the category the
// marker *denotes*; CategoryVariants is what says which categories point at
// it. The 💎 gem rows will appear here when they get imported
public enum SegmentCategory {
    AnyPercent,
    Cassette,
    TrueEnding,
    TrueEndingDts,
}

// how the categories are shown and walked through. The names are the sheet's
// own vocabulary, deliberately untranslated, and both the Mod Options slider
// and the cycle hotkey read them from here so the two never drift apart. Kept
// game-free like the rest of this file: a test checks the table still covers
// every enum value
public static class SegmentCategories {
    // indexed by SegmentCategory. "Any% Cassettes": the 5A and 6A cassettes are part of the any% run, so the
    // name says which run these segments belong to rather than pretending to
    // be a category of its own. The rule the split enforces is that no
    // category ever holds two segments starting at the same in-game
    // checkpoint — wider categories are described by what they *add*, and
    // fall back to the any% row everywhere they add nothing: "True Ending"
    // only names the hearts, since Core and Farewell start
    // at checkpoints no other category has a segment for. "True Ending DTS"
    // is that same run with the double-dash skip, which the sheet times
    // separately from Farewell's Start to Determination. The enum member
    // keeps its short name: it is what gets persisted in the settings file
    public static readonly string[] Names = ["Any%", "Any% Cassettes", "True Ending", "True Ending DTS"];

    public static string NameOf(SegmentCategory category) =>
        (int)category >= 0 && (int)category < Names.Length ? Names[(int)category] : category.ToString();

    // the hotkey walks the slider's order and wraps around; driven by the enum
    // rather than by Names so a category added without its label still cycles
    public static SegmentCategory Next(SegmentCategory category) =>
        (SegmentCategory)(((int)category + 1) % Enum.GetValues(typeof(SegmentCategory)).Length);
}

// what finishes a run of the segment: derived from the sheet's own
// naming vocabulary, evaluated by RunWatcher — SpeedrunTool's Number of Rooms
// plays no part anymore.
public enum EndCondition {
    // ends where the next in-game checkpoint starts; resolved at runtime from
    // AreaData (no next checkpoint ⇒ the chapter's completion ends the run)
    Checkpoint,
    // "📼 RTM" rows: the run ends the moment the cassette is collected (the
    // community convention for RTM segments — the menuing after the grab is
    // not gameplay and is never timed by the room timer)
    Cassette,
    // "💙 RTM" rows: same, for the crystal heart
    Heart,
}

// parsed practice sheet: one block of checkpoint segments merged from the
// three imported tabs ("A Sides Standards", "B Sides Standards" and
// "Farewell Standards"), with a header of tier columns ("Hidden",
// "WR", "Gold", "Pink", "Purple 1", ... "Unranked")
public class SheetData {
    /// The one block Parse builds, checkpoint segments of all three tabs
    /// merged under one header; null when nothing parsed.
    public SheetBlock CheckpointBlock { get; private set; }

    /// The SheetRows keys none of the parsed tabs had. A row the
    /// sheet renamed misses the allowlist without a sound and drops out of the
    /// sliders, the tier and auto-detection; this is how it gets noticed.
    /// Covers only what was given: parse two tabs, and the third one's rows
    /// are all here.
    public readonly List<(string Chapter, string Name)> MissingRows = [];

    public int SegmentCount => CheckpointBlock?.Segments.Count ?? 0;

    /// The tier a time reaches: the first column, in sheet order, whose
    /// threshold the time is at or under, and "Unranked" past every one.
    /// Sheet order, not the fastest threshold: where a stale WR is slower than
    /// Gold, a time under the WR reads WR. A zero threshold (Hidden, and some
    /// WR cells) and an empty cell never match.
    internal static string TierOf(List<string> columns, List<TimeSpan?> thresholds, TimeSpan time) {
        for (int i = 0; i < thresholds.Count && i < columns.Count; i++) {
            if (thresholds[i] is { } threshold && threshold > TimeSpan.Zero && time <= threshold) {
                return columns[i];
            }
        }

        return "Unranked";
    }

    // never throws on malformed content: unparseable cells become null times,
    // rows outside any block or absent from the SheetRows allowlist are skipped.
    // Segments from the three tabs land in one merged block, in tab row order
    // (A, then B, then Farewell), so the B-side rows of the folded chapters
    // follow their A-side ones like on the old sheet and Farewell closes the
    // chapter list. All three tabs share the same tier columns; the merged
    // header is taken from the first tab that has one. Farewell is the tab
    // whose rows carry no chapter cell (see ParseBlocks)
    public static SheetData Parse(string aSidesCsv, string bSidesCsv, string farewellCsv = null) {
        SheetData data = new();
        SheetBlock merged = null;
        HashSet<(StandardsTab, string, string)> imported = [];

        foreach ((string csv, StandardsTab tab, string implicitChapter) in new[] {
                     (aSidesCsv, StandardsTab.ASides, (string)null),
                     (bSidesCsv, StandardsTab.BSides, null),
                     (farewellCsv, StandardsTab.Farewell, "Farewell"),
                 }) {
            if (string.IsNullOrWhiteSpace(csv)) {
                continue;
            }

            foreach (SheetBlock raw in ParseBlocks(csv, implicitChapter)) {
                // the "Chapter Times ..." blocks have no Checkpoint column —
                // and neither has the Farewell tab, whose chapter is implicit
                if (!raw.HasCheckpoints && implicitChapter == null) {
                    continue;
                }

                if (merged == null) {
                    merged = new SheetBlock("Checkpoints", raw.TierStart, hasCheckpoints: true);
                    merged.Columns.AddRange(raw.Columns);
                    data.CheckpointBlock = merged;
                }

                foreach (SheetSegment segment in raw.Segments) {
                    if (SheetRows.TryRead(tab, segment.Chapter, segment.Name, out SheetRow row)) {
                        imported.Add((tab, segment.Chapter, segment.Name));
                        merged.Segments.Add(new SheetSegment(row.Chapter, row.Name,
                            Realigned(segment.Times, merged.Columns.Count),
                            CategoryOf(segment.Name), EndConditionOf(segment.Name)));
                    }
                }
            }
        }

        foreach (SheetRow row in SheetRows.All) {
            if (!imported.Contains((row.Tab, row.SheetChapter, row.Label))) {
                data.MissingRows.Add((row.SheetChapter, row.Label));
            }
        }

        return data;
    }

    // one segment's times, stretched (or cut) to the merged block's tier
    // columns. The tabs do not all end on the same column: Farewell stops at
    // "Red 3" where the A and B tabs have a trailing "Unranked". Since
    // "Unranked" is a column with no values anyway (the tier beyond Red 3),
    // padding with nulls is exactly what a missing column means — and it keeps
    // the block's promise that Times is indexable by Columns
    private static List<TimeSpan?> Realigned(List<TimeSpan?> times, int columns) {
        List<TimeSpan?> aligned = new(columns);
        for (int i = 0; i < columns; i++) {
            aligned.Add(i < times.Count ? times[i] : null);
        }

        return aligned;
    }

    // the raw sheet name carries the category as an emoji marker; matched with
    // Contains — the sheet's own spacing around the marker is inconsistent
    // ("📼 RTM", "📼Clear"), so no exact-name matching here. The Farewell tab
    // marks its double-dash-skip rows with a plain " DTS" suffix instead
    // ("Start DTS"), which is exact enough to match on: the four SoB/IL totals
    // that start with "DTS" are not imported anyway
    internal static SegmentCategory CategoryOf(string rawName) {
        string name = rawName.TrimEnd();
        if (name.Contains("📼")) {
            return SegmentCategory.Cassette;
        }

        if (name.EndsWith(" DTS", StringComparison.Ordinal)) {
            return SegmentCategory.TrueEndingDts;
        }

        // the heart rows are run by both True Ending variants; the category
        // here is only the one the marker denotes, CategoryVariants lists the
        // categories that actually point at the row
        return name.Contains("💙") ? SegmentCategory.TrueEnding : SegmentCategory.AnyPercent;
    }

    // the end of the run is in the raw name too, and "RTM" and "RC" are the
    // only things that end one early: the sheet's markers for "collect, then
    // return to map" or "restart the chapter", and the community convention is
    // that the segment stops at the collect (the menuing after it is not
    // gameplay and is never timed). Every other
    // row runs to the end of its segment — the next in-game checkpoint, or
    // the chapter itself when there is none, which RunWatcher resolves at
    // runtime with no help from here.
    // A "Clear" suffix on a checkpoint row is *not* the chapter's completion,
    // whatever it reads like: "Shrine 💙 Clear" (27.5s) cannot contain Old
    // Trail and Cliff Face (78s of run after it), and the sheet's own chapter
    // totals go up by exactly what the heart detour costs that one segment.
    // It means "collect it and keep going", as opposed to the "Shrine 💙 RTM"
    // row next to it (owner confirmed).
    // Combined "💙+📼" RTM rows default to Cassette until they are actually
    // imported and their route settles which comes last
    internal static EndCondition EndConditionOf(string rawName) {
        string name = rawName.TrimEnd();
        // RTM (return to map) and RC (restart chapter) both end the run at the
        // collection: what follows either one is menuing, which the room timer
        // never counts. RC is on exactly one row of the whole sheet,
        // "2a Start 💙 RC", so reading it changes no segment that already works
        if (!name.EndsWith("RTM", StringComparison.Ordinal)
            && !name.EndsWith("RC", StringComparison.Ordinal)) {
            return EndCondition.Checkpoint;
        }

        if (name.Contains("📼")) {
            return EndCondition.Cassette;
        }

        return name.Contains("💙") ? EndCondition.Heart : EndCondition.Checkpoint;
    }

    // raw pass shared by the three tabs: split the CSV into blocks of segments,
    // one block per header row, keeping the sheet's own chapter/checkpoint
    // names. internal rather than private so the tests can check the SheetRows
    // allowlist against the raw rows of the sheet.
    // implicitChapter is for the Farewell tab, which has no Chapter column at
    // all: its rows read like the "Chapter Times" ones (a single label column)
    // but each label is a checkpoint of that one chapter
    internal static List<SheetBlock> ParseBlocks(string csvText, string implicitChapter = null) {
        List<SheetBlock> blocks = [];
        SheetBlock currentBlock = null;
        string currentChapter = null;

        foreach (string[] row in Csv.Parse(csvText)) {
            if (IsEmpty(row)) {
                continue;
            }

            // a header row introduces a new block: first cell is the block title
            // ("Chapter", "Chapter Times (CP)"), then an optional "Checkpoint"
            // column, then the tier column labels
            int tierStart = TierStart(row);
            if (tierStart > 0) {
                currentBlock = new SheetBlock(row[0].Trim(), tierStart, row[1].Trim() == "Checkpoint");
                currentChapter = null;
                for (int i = tierStart; i < row.Length; i++) {
                    string label = row[i].Trim();
                    if (label.Length > 0) {
                        currentBlock.Columns.Add(label);
                    }
                }

                blocks.Add(currentBlock);
                continue;
            }

            if (currentBlock == null) {
                continue;
            }

            // the chapter cell is only filled on the first checkpoint of a
            // chapter (merged cells export as empty cells below), so carry it
            if (row[0].Trim().Length > 0) {
                currentChapter = row[0].Trim();
            }

            string name = currentBlock.HasCheckpoints && row.Length > 1 ? row[1].Trim() : currentChapter;
            if (string.IsNullOrEmpty(name) || currentChapter == null) {
                continue;
            }

            SheetSegment segment = new(implicitChapter ?? currentChapter, name);
            for (int i = currentBlock.TierStart; i < currentBlock.TierStart + currentBlock.Columns.Count; i++) {
                segment.Times.Add(i < row.Length ? TryParseTime(row[i]) : null);
            }

            currentBlock.Segments.Add(segment);
        }

        return blocks;
    }

    // header rows are marked by the fixed first tier columns "Hidden","WR",
    // sitting at index 1 (chapter-only layout) or 2 (chapter+checkpoint layout);
    // returns the index of "Hidden", or 0 if the row is not a header
    private static int TierStart(string[] row) {
        for (int i = 1; i <= 2; i++) {
            if (row.Length > i + 1 && row[i].Trim() == "Hidden" && row[i + 1].Trim() == "WR") {
                return i;
            }
        }

        return 0;
    }

    private static bool IsEmpty(string[] row) {
        foreach (string cell in row) {
            if (cell.Trim().Length > 0) {
                return false;
            }
        }

        return true;
    }

    // accepts the sheet's mixed formats: "28", "28.1", "00:56", "1:05.5", "24:06.802"
    public static TimeSpan? TryParseTime(string cell) {
        // null is not only a CSV thing any more: the export asks about a cell
        // the sheet may not have at all, and "no cell" parses like an empty one
        if (string.IsNullOrWhiteSpace(cell)) {
            return null;
        }

        string text = cell.Trim();

        string[] parts = text.Split(':');
        if (parts.Length > 3) {
            return null;
        }

        // past the leading field, each one counts minutes or seconds and stays
        // under 60 ("1:75" is a typo, not 2:15); only the seconds carry a fraction
        double totalSeconds = 0;
        for (int i = 0; i < parts.Length; i++) {
            bool last = i == parts.Length - 1;
            NumberStyles style = last ? NumberStyles.AllowDecimalPoint : NumberStyles.None;
            if (!double.TryParse(parts[i], style, CultureInfo.InvariantCulture, out double value)
                || value < 0 || (i > 0 && value >= 60)) {
                return null;
            }

            totalSeconds = totalSeconds * 60 + value;
        }

        // via ticks: TimeSpan.FromSeconds rounds to milliseconds with double
        // imprecision (90.576 s would become 1:30.575)
        return TimeSpan.FromTicks((long)Math.Round(totalSeconds * TimeSpan.TicksPerSecond));
    }
}

public class SheetBlock(string name, int tierStart, bool hasCheckpoints) {
    public readonly string Name = name;
    // column index of the first tier ("Hidden"); segment times start there too
    public readonly int TierStart = tierStart;
    // true when segments are individual checkpoints grouped under a chapter,
    // false when each segment is a whole chapter ("Chapter Times" blocks)
    public readonly bool HasCheckpoints = hasCheckpoints;
    public readonly List<string> Columns = [];
    public readonly List<SheetSegment> Segments = [];

    // distinct chapters in sheet order ("Prologue", "1a", … "5a/b", …)
    public List<string> Chapters() {
        List<string> chapters = [];
        foreach (SheetSegment segment in Segments) {
            if (!chapters.Contains(segment.Chapter)) {
                chapters.Add(segment.Chapter);
            }
        }

        return chapters;
    }

    // checkpoint names repeat across chapters ("Start" in nearly all of
    // them), so checkpoints are always addressed by (chapter, name)
    /// The segment of that chapter and name, or null. Callers address segments
    /// by name, never by reference: SheetImporter.Data is reassigned from a
    /// worker, and the new instances are not equal to the old ones.
    public SheetSegment Find(string chapter, string name) {
        foreach (SheetSegment segment in Segments) {
            if (segment.Chapter == chapter && segment.Name == name) {
                return segment;
            }
        }

        return null;
    }

    public List<SheetSegment> Checkpoints(string chapter) {
        List<SheetSegment> checkpoints = [];
        foreach (SheetSegment segment in Segments) {
            if (segment.Chapter == chapter) {
                checkpoints.Add(segment);
            }
        }

        return checkpoints;
    }
}

public class SheetSegment(string chapter, string name, List<TimeSpan?> times = null,
    SegmentCategory category = SegmentCategory.AnyPercent, EndCondition end = EndCondition.Checkpoint) {
    // owning chapter; equals Name in chapter-only blocks
    public readonly string Chapter = chapter;
    public readonly string Name = name;
    // aligned with the owning block's Columns; null = empty or unparseable cell
    public readonly List<TimeSpan?> Times = times ?? [];
    // derived from the raw sheet name's marker at import (raw blocks keep the
    // defaults: their names still carry the marker itself). Only the tests read
    // Category: the mod picks a variant through CategoryVariants, not this
    public readonly SegmentCategory Category = category;
    public readonly EndCondition End = end;
}

// minimal RFC 4180 parser: quoted fields, "" escapes, \r\n or \n line ends
internal static class Csv {
    public static List<string[]> Parse(string text) {
        List<string[]> rows = [];
        List<string> fields = [];
        System.Text.StringBuilder field = new();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (inQuotes) {
                if (c == '"') {
                    if (i + 1 < text.Length && text[i + 1] == '"') {
                        field.Append('"');
                        i++;
                    } else {
                        inQuotes = false;
                    }
                } else {
                    field.Append(c);
                }
            } else if (c == '"') {
                inQuotes = true;
            } else if (c == ',') {
                fields.Add(field.ToString());
                field.Clear();
            } else if (c == '\n' || c == '\r') {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') {
                    i++;
                }

                fields.Add(field.ToString());
                field.Clear();
                rows.Add(fields.ToArray());
                fields.Clear();
            } else {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0) {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }

        return rows;
    }
}
