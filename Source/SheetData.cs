using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.SpeedrunSheet;

// parsed practice sheet: one block of checkpoint segments merged from the two
// imported tabs ("A Sides Standards" + "B Sides Standards"), with a header of
// tier columns ("Hidden", "WR", "Gold", "Pink", "Purple 1", ... "Unranked")
public class SheetData {
    public readonly List<SheetBlock> Blocks = [];

    public int SegmentCount {
        get {
            int count = 0;
            foreach (SheetBlock block in Blocks) {
                count += block.Segments.Count;
            }

            return count;
        }
    }

    // the block whose segments are individual checkpoints (the selectable ones)
    public SheetBlock CheckpointBlock {
        get {
            foreach (SheetBlock block in Blocks) {
                if (block.HasCheckpoints) {
                    return block;
                }
            }

            return Blocks.Count > 0 ? Blocks[0] : null;
        }
    }

    // raw (chapter, checkpoint) of the sheet -> (chapter, name) of the mod.
    // Deliberately a hardcoded allowlist, no name normalization (owner decision
    // 2026-07-18, renewed 2026-08-05 for the new sheet): only the checkpoints
    // the mod supports are imported — the heart/cassette/gem emoji variants
    // (no room counts known) and the IL rows are for later. Two cassette
    // exceptions requested by the owner: "Depths 📼 RTM" and "Hollows 📼 RTM"
    // become the Depths Tape / Hollows Tape checkpoints. Like the old sheet,
    // the two route choices are folded into single "5a/b"/"6a/b" chapters, the
    // chapter echo is dropped from names ("1a Start" -> "Start") except the
    // side-disambiguating ones ("5a Start"), and names shared by both sides
    // keep a side prefix ("6a Rock Bottom"/"6b Rock Bottom")
    private static readonly Dictionary<(string Chapter, string Name), (string Chapter, string Name)> Import = new() {
        // A Sides Standards: "<X>a CP" groups (the "<X>a IL" groups and the
        // full-5A route past Depths — Unravelling, Search, Rescue — are not
        // imported yet)
        [("Prologue", "Granny")] = ("Prologue", "Granny"),
        [("1a CP", "1a Start")] = ("1a", "Start"),
        [("1a CP", "Crossing")] = ("1a", "Crossing"),
        [("1a CP", "Chasm")] = ("1a", "Chasm"),
        [("2a CP", "2a Start")] = ("2a", "Start"),
        [("2a CP", "Intervention")] = ("2a", "Intervention"),
        [("2a CP", "Awake")] = ("2a", "Awake"),
        [("3a CP", "3a Start")] = ("3a", "Start"),
        [("3a CP", "Huge Mess")] = ("3a", "Huge Mess"),
        [("3a CP", "Elevator Shaft")] = ("3a", "Elevator Shaft"),
        [("3a CP", "Presidential Suite")] = ("3a", "Presidential Suite"),
        [("4a CP", "4a Start")] = ("4a", "Start"),
        [("4a CP", "Shrine")] = ("4a", "Shrine"),
        [("4a CP", "Old Trail")] = ("4a", "Old Trail"),
        [("4a CP", "Cliff Face")] = ("4a", "Cliff Face"),
        [("5a CP", "5a Start")] = ("5a/b", "5a Start"),
        [("5a CP", "Depths")] = ("5a/b", "Depths"),
        [("5a CP", "Depths 📼 RTM")] = ("5a/b", "Depths Tape"),
        [("6a CP", "6a Start")] = ("6a/b", "6a Start"),
        [("6a CP", "Lake")] = ("6a/b", "Lake"),
        [("6a CP", "Hollows")] = ("6a/b", "Hollows"),
        [("6a CP", "Hollows 📼 RTM")] = ("6a/b", "Hollows Tape"),
        [("6a CP", "Reflection")] = ("6a/b", "Reflection"),
        [("6a CP", "Rock Bottom")] = ("6a/b", "6a Rock Bottom"),
        [("6a CP", "Resolution")] = ("6a/b", "Resolution"),
        [("7a CP", "0m")] = ("7a", "0m"),
        [("7a CP", "500m")] = ("7a", "500m"),
        [("7a CP", "1000m")] = ("7a", "1000m"),
        [("7a CP", "1500m")] = ("7a", "1500m"),
        [("7a CP", "2000m")] = ("7a", "2000m"),
        [("7a CP", "2500m")] = ("7a", "2500m"),
        [("7a CP", "3000m")] = ("7a", "3000m"),
        // B Sides Standards: only the any% route's two B-sides
        [("5b", "5b Start")] = ("5a/b", "5b Start"),
        [("5b", "Central Chamber")] = ("5a/b", "Central Chamber"),
        [("5b", "Through The Mirror")] = ("5a/b", "Through The Mirror"),
        [("5b", "Mix Master")] = ("5a/b", "Mix Master"),
        [("6b", "6b Start")] = ("6a/b", "6b Start"),
        [("6b", "Falling")] = ("6a/b", "Falling"),
        [("6b", "Rock Bottom")] = ("6a/b", "6b Rock Bottom"),
        [("6b", "Reprieve")] = ("6a/b", "Reprieve"),
    };

    // never throws on malformed content: unparseable cells become null times,
    // rows outside any block or absent from the Import allowlist are skipped.
    // Segments from both tabs land in one merged block, in tab row order (A
    // then B), so the B-side rows of the folded chapters follow their A-side
    // ones like on the old sheet. Both tabs share the same tier columns; the
    // merged header is taken from the first tab that has one
    public static SheetData Parse(string aSidesCsv, string bSidesCsv) {
        SheetData data = new();
        SheetBlock merged = null;

        foreach (string csv in new[] { aSidesCsv, bSidesCsv }) {
            if (string.IsNullOrWhiteSpace(csv)) {
                continue;
            }

            foreach (SheetBlock raw in ParseBlocks(csv)) {
                // the "Chapter Times ..." blocks have no Checkpoint column
                if (!raw.HasCheckpoints) {
                    continue;
                }

                if (merged == null) {
                    merged = new SheetBlock("Checkpoints", raw.TierStart, hasCheckpoints: true);
                    merged.Columns.AddRange(raw.Columns);
                    data.Blocks.Add(merged);
                }

                foreach (SheetSegment segment in raw.Segments) {
                    if (Import.TryGetValue((segment.Chapter, segment.Name), out (string Chapter, string Name) target)) {
                        merged.Segments.Add(new SheetSegment(target.Chapter, target.Name, segment.Times));
                    }
                }
            }
        }

        return data;
    }

    // raw pass shared by both tabs: split the CSV into blocks of segments, one
    // block per header row, keeping the sheet's own chapter/checkpoint names
    private static List<SheetBlock> ParseBlocks(string csvText) {
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

            SheetSegment segment = new(currentChapter, name);
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
        string text = cell.Trim();
        if (text.Length == 0) {
            return null;
        }

        string[] parts = text.Split(':');
        if (parts.Length > 3) {
            return null;
        }

        double totalSeconds = 0;
        foreach (string part in parts) {
            if (!double.TryParse(part, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value) || value < 0) {
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

public class SheetSegment(string chapter, string name, List<TimeSpan?> times = null) {
    // owning chapter; equals Name in chapter-only blocks
    public readonly string Chapter = chapter;
    public readonly string Name = name;
    // aligned with the owning block's Columns; null = empty or unparseable cell
    public readonly List<TimeSpan?> Times = times ?? [];
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
