using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// One row srs imports, described once for both documents: the Standards row
/// it is read from (Tab, SheetChapter, Label) and the entry row it is written
/// to (TargetOf). Scope, Chapter and Name are srs's own address, and Anchor is
/// the game checkpoint a run of it starts at, in the scope's English names.
/// Target is set only where the entry tab genuinely spells the row otherwise.
internal readonly record struct SheetRow(
    StandardsTab Tab, string SheetChapter, string Label,
    string Scope, string Chapter, string Name, string Anchor,
    SheetRowRef? Target = null);

/// The rows srs imports. A hardcoded allowlist with no name normalisation
/// (owner decision): a row the sheet renames is reported by SheetData.MissingRows.
/// The two documents are described by one table, but they stay two documents,
/// corrected on their own dates: a difference between them is a Target
/// override, never an edit of the label.
internal static class SheetRows {
    private const StandardsTab A = StandardsTab.ASides;
    private const StandardsTab B = StandardsTab.BSides;
    private const StandardsTab C = StandardsTab.CSides;
    private const StandardsTab F = StandardsTab.Farewell;

    // kept as escaped code points so this source stays ASCII
    private const string Heart = "\U0001F499";
    private const string Tape = "\U0001F4FC";

    // in sheet order: A Sides, B Sides, C Sides, then Farewell. The emoji survive in
    // Label only: ActiveFont skips a glyph its atlas lacks, so Name never has one
    internal static readonly SheetRow[] All = [
        new(A, "Prologue", "Granny", "Prologue", "Prologue", "Granny", "Start"),
        new(A, "1a CP", "1a Start", "1a", "1a", "Start", "Start"),
        new(A, "1a CP", "Crossing", "1a", "1a", "Crossing", "Crossing"),
        new(A, "1a CP", "Chasm", "1a", "1a", "Chasm", "Chasm"),
        // an IL's entry row sits under the bare chapter ("1a")
        new(A, "1a IL", "Clear", "1a", "1a", "IL", "Start", new(SheetLabels.TabASides, "1a", "Clear")),
        new(A, "2a CP", "2a Start", "2a", "2a", "Start", "Start"),
        new(A, "2a CP", "2a Start " + Heart + " RC", "2a", "2a", "Start Heart RC", "Start"),
        new(A, "2a CP", "Intervention", "2a", "2a", "Intervention", "Intervention"),
        new(A, "2a CP", "Awake", "2a", "2a", "Awake", "Awake"),
        new(A, "2a IL", "Clear", "2a", "2a", "IL", "Start", new(SheetLabels.TabASides, "2a", "Clear")),
        new(A, "3a CP", "3a Start", "3a", "3a", "Start", "Start"),
        new(A, "3a CP", "Huge Mess", "3a", "3a", "Huge Mess", "Huge Mess"),
        new(A, "3a CP", "Huge Mess " + Heart, "3a", "3a", "Huge Mess Heart", "Huge Mess"),
        new(A, "3a CP", "Elevator Shaft", "3a", "3a", "Elevator Shaft", "Elevator Shaft"),
        new(A, "3a CP", "Presidential Suite", "3a", "3a", "Presidential Suite", "Presidential Suite"),
        new(A, "3a IL", "Clear", "3a", "3a", "IL", "Start", new(SheetLabels.TabASides, "3a", "Clear")),
        new(A, "4a CP", "4a Start", "4a", "4a", "Start", "Start"),
        new(A, "4a CP", "Shrine", "4a", "4a", "Shrine", "Shrine"),
        new(A, "4a CP", "Shrine " + Heart + " Clear", "4a", "4a", "Shrine Heart", "Shrine"),
        new(A, "4a CP", "Old Trail", "4a", "4a", "Old Trail", "Old Trail"),
        new(A, "4a CP", "Cliff Face", "4a", "4a", "Cliff Face", "Cliff Face"),
        new(A, "4a IL", "Clear", "4a", "4a", "IL", "Start", new(SheetLabels.TabASides, "4a", "Clear")),
        // the two route choices are folded into "5a/b" and "6a/b", and names
        // both sides share keep a side prefix ("6a Rock Bottom")
        new(A, "5a CP", "5a Start", "5a", "5a/b", "5a Start", "Start"),
        new(A, "5a CP", "Depths", "5a", "5a/b", "Depths", "Depths"),
        new(A, "5a CP", "Depths " + Tape + " RTM", "5a", "5a/b", "Depths Tape", "Depths"),
        new(A, "5a CP", "Unravelling", "5a", "5a/b", "Unravelling", "Unravelling"),
        new(A, "5a CP", "Search", "5a", "5a/b", "Search", "Search"),
        new(A, "5a CP", "Rescue", "5a", "5a/b", "Rescue", "Rescue"),
        new(A, "5a IL", "Clear", "5a", "5a/b", "5a IL", "Start", new(SheetLabels.TabASides, "5a", "Clear")),
        new(A, "6a CP", "6a Start", "6a", "6a/b", "6a Start", "Start"),
        new(A, "6a CP", "Lake", "6a", "6a/b", "Lake", "Lake"),
        new(A, "6a CP", "Hollows", "6a", "6a/b", "Hollows", "Hollows"),
        new(A, "6a CP", "Hollows " + Tape + " RTM", "6a", "6a/b", "Hollows Tape", "Hollows"),
        new(A, "6a CP", "Reflection", "6a", "6a/b", "Reflection", "Reflection"),
        new(A, "6a CP", "Rock Bottom", "6a", "6a/b", "6a Rock Bottom", "Rock Bottom"),
        new(A, "6a CP", "Resolution", "6a", "6a/b", "Resolution", "Resolution"),
        new(A, "6a IL", "Clear", "6a", "6a/b", "6a IL", "Start", new(SheetLabels.TabASides, "6a", "Clear")),
        new(A, "7a CP", "7a Start", "7a", "7a", "7a Start", "Start"),
        new(A, "7a CP", "500m", "7a", "7a", "500m", "500 M"),
        new(A, "7a CP", "1000m", "7a", "7a", "1000m", "1000 M"),
        new(A, "7a CP", "1500m", "7a", "7a", "1500m", "1500 M"),
        new(A, "7a CP", "2000m", "7a", "7a", "2000m", "2000 M"),
        new(A, "7a CP", "2500m", "7a", "7a", "2500m", "2500 M"),
        new(A, "7a CP", "3000m", "7a", "7a", "3000m", "3000 M"),
        new(A, "7a IL", "Clear", "7a", "7a", "7a IL", "Start", new(SheetLabels.TabASides, "7a", "Clear")),
        new(A, "8a CP", "8a Start", "8a", "8a", "Start", "Start"),
        new(A, "8a CP", "Into the Core", "8a", "8a", "Into the Core", "Into the Core"),
        new(A, "8a CP", "Hot and Cold", "8a", "8a", "Hot and Cold", "Hot and Cold"),
        // the sheet cuts the game's one "Heart of the Mountain" checkpoint in two;
        // the second half is virtual (SegmentAutoDetect.SplitCheckpoints)
        new(A, "8a CP", "HotM Vertical", "8a", "8a", "HotM Vertical", "Heart of the Mountain"),
        new(A, "8a CP", "HotM Horizontal", "8a", "8a", "HotM Horizontal", "HotM Horizontal"),
        new(A, "8a IL", "Clear", "8a", "8a", "IL", "Start", new(SheetLabels.TabASides, "8a", "Clear")),
        new(B, "5b", "5b Start", "5b", "5a/b", "5b Start", "Start"),
        new(B, "5b", "Central Chamber", "5b", "5a/b", "Central Chamber", "Central Chamber"),
        new(B, "5b", "Through the Mirror", "5b", "5a/b", "Through the Mirror", "Through the Mirror"),
        new(B, "5b", "Mix Master", "5b", "5a/b", "Mix Master", "Mix Master"),
        new(B, "6b", "6b Start", "6b", "6a/b", "6b Start", "Start"),
        // the sheet's name for 6B's Reflection
        new(B, "6b", "Falling", "6b", "6a/b", "Falling", "Reflection"),
        new(B, "6b", "Rock Bottom", "6b", "6a/b", "6b Rock Bottom", "Rock Bottom"),
        new(B, "6b", "Reprieve", "6b", "6a/b", "Reprieve", "Reprieve"),
        // a C-side is one row, named after its chapter: the tab has no
        // Checkpoint column, and the run is the whole chapter
        new(C, "1c", "1c", "1c", "1c", "1c", "Start"),
        new(C, "2c", "2c", "2c", "2c", "2c", "Start"),
        new(C, "3c", "3c", "3c", "3c", "3c", "Start"),
        new(C, "4c", "4c", "4c", "4c", "4c", "Start"),
        new(C, "5c", "5c", "5c", "5c", "5c", "Start"),
        new(C, "6c", "6c", "6c", "6c", "6c", "Start"),
        new(C, "7c", "7c", "7c", "7c", "7c", "Start"),
        new(C, "8c", "8c", "8c", "8c", "8c", "Start"),
        // the Farewell tab has no Chapter column: Parse reads it under "Farewell"
        new(F, "Farewell", "Start", "Farewell", "Farewell", "Start", "Start"),
        new(F, "Farewell", "Singular", "Farewell", "Farewell", "Singular", "Singular"),
        new(F, "Farewell", "Power Source", "Farewell", "Farewell", "Power Source", "Power Source"),
        new(F, "Farewell", "Remembered", "Farewell", "Farewell", "Remembered", "Remembered"),
        new(F, "Farewell", "Event Horizon", "Farewell", "Farewell", "Event Horizon", "Event Horizon"),
        new(F, "Farewell", "Determination", "Farewell", "Farewell", "Determination", "Determination"),
        new(F, "Farewell", "Start DTS", "Farewell", "Farewell", "Start DTS", "Start"),
        new(F, "Farewell", "Singular DTS", "Farewell", "Farewell", "Singular DTS", "Singular"),
        new(F, "Farewell", "Power Source DTS", "Farewell", "Farewell", "Power Source DTS", "Power Source"),
        new(F, "Farewell", "Remembered DTS", "Farewell", "Farewell", "Remembered DTS", "Remembered"),
        new(F, "Farewell", "Event Horizon DTS", "Farewell", "Farewell", "Event Horizon DTS", "Event Horizon"),
        new(F, "Farewell", "Determination DTS", "Farewell", "Farewell", "Determination DTS", "Determination"),
        new(F, "Farewell", "Stubbornness", "Farewell", "Farewell", "Stubbornness", "Stubbornness"),
        new(F, "Farewell", "Reconciliation", "Farewell", "Farewell", "Reconciliation", "Reconciliation"),
        new(F, "Farewell", "Farewell", "Farewell", "Farewell", "Farewell", "Farewell"),
        // the entry tab drops the " IL"
        new(F, "Farewell", "DTS IL", "Farewell", "Farewell", "DTS IL", "Start", new(SheetLabels.TabFarewell, "", "DTS")),
        new(F, "Farewell", "No DTS IL", "Farewell", "Farewell", "No DTS IL", "Start", new(SheetLabels.TabFarewell, "", "No DTS")),
    ];

    private static readonly Dictionary<(StandardsTab, string, string), SheetRow> byStandards = [];
    private static readonly Dictionary<(string, string), SheetRow> byAddress = [];

    // Add, not the indexer: a row keyed twice throws at type load, so a
    // duplicate cannot hide behind the later entry
    static SheetRows() {
        foreach (SheetRow row in All) {
            byStandards.Add((row.Tab, row.SheetChapter, row.Label), row);
            byAddress.Add((row.Chapter, row.Name), row);
        }
    }

    public static bool TryRead(StandardsTab tab, string sheetChapter, string label, out SheetRow row) =>
        byStandards.TryGetValue((tab, sheetChapter, label), out row);

    public static bool TryFind(string chapter, string name, out SheetRow row) =>
        byAddress.TryGetValue((chapter, name), out row);

    public static SheetRowRef TargetOf(SheetRow row) => row.Target ?? DefaultTarget(row);

    /// Same label on the matching entry tab, under the Standards chapter without
    /// its " CP" suffix ("1a CP" -> "1a"); the Farewell tab has no chapter column.
    internal static SheetRowRef DefaultTarget(SheetRow row) {
        string chapter = row.Tab == StandardsTab.Farewell ? ""
            : row.SheetChapter.EndsWith(" CP", System.StringComparison.Ordinal) ? row.SheetChapter[..^3]
            : row.SheetChapter;
        return new SheetRowRef(StandardsTabs.Of(row.Tab).EntryTab, chapter, row.Label);
    }
}
