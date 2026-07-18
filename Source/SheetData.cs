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

    // never throws on malformed content: unparseable cells become null times,
    // rows outside any block are skipped
    public static SheetData Parse(string csvText) {
        SheetData data = new();
        SheetBlock currentBlock = null;

        foreach (string[] row in Csv.Parse(csvText)) {
            if (IsEmpty(row)) {
                continue;
            }

            // a header row introduces a new block: first cell is the block title
            // ("Chapter", "Chapter Times"), the rest are the tier column labels
            if (IsHeader(row)) {
                currentBlock = new SheetBlock(row[0].Trim());
                for (int i = 1; i < row.Length; i++) {
                    string label = row[i].Trim();
                    if (label.Length > 0) {
                        currentBlock.Columns.Add(label);
                    }
                }

                data.Blocks.Add(currentBlock);
                continue;
            }

            if (currentBlock == null || row[0].Trim().Length == 0) {
                continue;
            }

            SheetSegment segment = new(row[0].Trim());
            for (int i = 1; i <= currentBlock.Columns.Count; i++) {
                segment.Times.Add(i < row.Length ? TryParseTime(row[i]) : null);
            }

            currentBlock.Segments.Add(segment);
        }

        return data;
    }

    // the sheet marks header rows by their fixed first tier columns
    private static bool IsHeader(string[] row) {
        return row.Length > 2 && row[1].Trim() == "Hidden" && row[2].Trim() == "WR";
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

public class SheetBlock(string name) {
    public readonly string Name = name;
    public readonly List<string> Columns = [];
    public readonly List<SheetSegment> Segments = [];
}

public class SheetSegment(string name) {
    public readonly string Name = name;
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
