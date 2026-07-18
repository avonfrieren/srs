using System;
using System.Collections.Generic;
using System.Globalization;

namespace Celeste.Mod.SpeedrunSheet;

// parsed practice sheet: blocks of segments, each block with its own header of
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

    // the block whose segments are individual checkpoints (the selectable ones);
    // falls back to the first block for chapter-only sheets, where each segment
    // is its own single-checkpoint chapter
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

    // never throws on malformed content: unparseable cells become null times,
    // rows outside any block are skipped
    public static SheetData Parse(string csvText) {
        SheetData data = new();
        SheetBlock currentBlock = null;
        string currentChapter = null;
        string currentSide = null;
        Dictionary<SheetSegment, string> sides = new();

        foreach (string[] row in Csv.Parse(csvText)) {
            if (IsEmpty(row)) {
                continue;
            }

            // a header row introduces a new block: first cell is the block title
            // ("Chapter", "Chapter Times"), then an optional "Checkpoint" column,
            // then the tier column labels
            int tierStart = TierStart(row);
            if (tierStart > 0) {
                currentBlock = new SheetBlock(row[0].Trim(), tierStart, row[1].Trim() == "Checkpoint");
                currentChapter = null;
                currentSide = null;
                for (int i = tierStart; i < row.Length; i++) {
                    string label = row[i].Trim();
                    if (label.Length > 0) {
                        currentBlock.Columns.Add(label);
                    }
                }

                data.Blocks.Add(currentBlock);
                continue;
            }

            if (currentBlock == null) {
                continue;
            }

            // the chapter cell is only filled on the first checkpoint of a
            // chapter (merged cells export as empty cells below), so carry it
            if (row[0].Trim().Length > 0) {
                currentChapter = row[0].Trim();
                currentSide = null;
                // "6a Route" and "6b Route" are the two halves of one route
                // choice: fold them into a single "6a/b" chapter (the layout
                // the sheet itself uses for 5a/b), keeping the side around to
                // disambiguate checkpoint names shared by both routes
                if (currentChapter.EndsWith("a Route") || currentChapter.EndsWith("b Route")) {
                    currentSide = currentChapter[..^" Route".Length];
                    currentChapter = currentSide[..^1] + "a/b";
                }
            }

            string name = currentBlock.HasCheckpoints && row.Length > 1 ? row[1].Trim() : currentChapter;
            if (string.IsNullOrEmpty(name) || currentChapter == null) {
                continue;
            }

            // "Wake Up" rows time the wake-up animation, not a checkpoint
            if (name == "Wake Up") {
                continue;
            }

            // drop the chapter echoed in checkpoint names ("1a Start"):
            // checkpoints are addressed by (chapter, name), the echo adds
            // nothing (side prefixes like "5a Start" survive: they never
            // match the full chapter name "5a/b")
            if (name.StartsWith(currentChapter + " ", StringComparison.Ordinal)) {
                name = name[(currentChapter.Length + 1)..];
            }

            SheetSegment segment = new(currentChapter, name);
            if (currentSide != null) {
                sides[segment] = currentSide;
            }

            for (int i = currentBlock.TierStart; i < currentBlock.TierStart + currentBlock.Columns.Count; i++) {
                segment.Times.Add(i < row.Length ? TryParseTime(row[i]) : null);
            }

            currentBlock.Segments.Add(segment);
        }

        RestoreSides(data, sides);
        return data;
    }

    // inside a folded "6a/b" chapter both routes can use the same checkpoint
    // name ("Rock Bottom"); those get their side back ("6a Rock Bottom",
    // "6b Rock Bottom") so (chapter, name) stays a unique address
    private static void RestoreSides(SheetData data, Dictionary<SheetSegment, string> sides) {
        if (sides.Count == 0) {
            return;
        }

        foreach (SheetBlock block in data.Blocks) {
            Dictionary<string, int> counts = new();
            foreach (SheetSegment segment in block.Segments) {
                string key = segment.Chapter + "\n" + segment.Name;
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            foreach (SheetSegment segment in block.Segments) {
                if (counts[segment.Chapter + "\n" + segment.Name] > 1 && sides.TryGetValue(segment, out string side)) {
                    segment.Name = side + " " + segment.Name;
                }
            }
        }
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
    // false when each segment is a whole chapter (IL layout, "Chapter Times")
    public readonly bool HasCheckpoints = hasCheckpoints;
    public readonly List<string> Columns = [];
    public readonly List<SheetSegment> Segments = [];

    // distinct chapters in sheet order ("Prologue", "1a", … "6a Route", …)
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

public class SheetSegment(string chapter, string name) {
    // owning chapter; equals Name in chapter-only blocks
    public readonly string Chapter = chapter;
    // mutable: RestoreSides re-prefixes duplicated names after parsing
    public string Name = name;
    // aligned with the owning block's Columns; null = empty or unparseable cell
    public readonly List<TimeSpan?> Times = [];
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
