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

/// The rows srs imports: a hardcoded allowlist with no name normalisation, so a
/// row the sheet renames is reported by SheetData.MissingRows. A difference
/// between the two documents is a Target override, never an edit of the label.
internal static class SheetRows {
    private const StandardsTab A = StandardsTab.ASides;
    private const StandardsTab B = StandardsTab.BSides;
    private const StandardsTab C = StandardsTab.CSides;
    private const StandardsTab F = StandardsTab.Farewell;
    private const StandardsTab R = StandardsTab.Arb;

    // kept as escaped code points so this source stays ASCII
    private const string Heart = "\U0001F499";
    private const string Tape = "\U0001F4FC";
    private const string Berry = "\U0001F353";

    // in sheet order: A Sides, B Sides, C Sides, then Farewell, then the berry
    // tab. The emoji survive in Label only: ActiveFont skips a glyph its atlas lacks, so Name never has one
    internal static readonly SheetRow[] All = [
        new(A, "Prologue", "Granny", "Prologue", "Prologue", "Granny", "Start"),
        new(A, "1a CP", "1a Start", "1a", "1a", "Start", "Start"),
        new(A, "1a CP", "Crossing", "1a", "1a", "Crossing", "Crossing"),
        new(A, "1a CP", "Crossing " + Heart, "1a", "1a", "Crossing Heart", "Crossing"),
        new(A, "1a CP", "Chasm", "1a", "1a", "Chasm", "Chasm"),
        new(A, "1a CP", "Chasm " + Tape + " Clear", "1a", "1a", "Chasm Tape Clear", "Chasm"),
        new(A, "1a CP", "Chasm " + Tape + " RTM", "1a", "1a", "Chasm Tape RTM", "Chasm"),
        // an IL's entry row sits under the bare chapter ("1a")
        new(A, "1a IL", "Clear", "1a", "1a", "IL", "Start", new(SheetLabels.TabASides, "1a", "Clear")),
        new(A, "1a IL", Tape + " RTM", "1a", "1a", "IL Tape RTM", "Start", new(SheetLabels.TabASides, "1a", Tape + " RTM")),
        new(A, "1a IL", Tape + " Clear", "1a", "1a", "IL Tape Clear", "Start", new(SheetLabels.TabASides, "1a", Tape + " Clear")),
        new(A, "1a IL", Heart + "+" + Tape + " RTM", "1a", "1a", "IL Heart Tape RTM", "Start", new(SheetLabels.TabASides, "1a", Heart + "+" + Tape + " RTM")),
        new(A, "2a CP", "2a Start", "2a", "2a", "Start", "Start"),
        new(A, "2a CP", "2a Start " + Heart + " RC", "2a", "2a", "Start Heart RC", "Start"),
        new(A, "2a CP", "2a Start " + Tape + " Clear", "2a", "2a", "Start Tape Clear", "Start"),
        new(A, "2a CP", "2a Start " + Tape + " RTM", "2a", "2a", "Start Tape RTM", "Start"),
        new(A, "2a CP", "Intervention", "2a", "2a", "Intervention", "Intervention"),
        new(A, "2a CP", "Awake", "2a", "2a", "Awake", "Awake"),
        new(A, "2a IL", "Clear", "2a", "2a", "IL", "Start", new(SheetLabels.TabASides, "2a", "Clear")),
        new(A, "2a IL", Tape + " Clear", "2a", "2a", "IL Tape Clear", "Start", new(SheetLabels.TabASides, "2a", Tape + " Clear")),
        new(A, "3a CP", "3a Start", "3a", "3a", "Start", "Start"),
        new(A, "3a CP", "Huge Mess", "3a", "3a", "Huge Mess", "Huge Mess"),
        new(A, "3a CP", "Huge Mess " + Heart, "3a", "3a", "Huge Mess Heart", "Huge Mess"),
        new(A, "3a CP", "Elevator Shaft", "3a", "3a", "Elevator Shaft", "Elevator Shaft"),
        new(A, "3a CP", "Elevator Shaft " + Tape + " Clear", "3a", "3a", "Elevator Shaft Tape Clear", "Elevator Shaft"),
        new(A, "3a CP", "Elevator Shaft " + Tape + " RTM", "3a", "3a", "Elevator Shaft Tape RTM", "Elevator Shaft"),
        new(A, "3a CP", "Presidential Suite", "3a", "3a", "Presidential Suite", "Presidential Suite"),
        new(A, "3a IL", "Clear", "3a", "3a", "IL", "Start", new(SheetLabels.TabASides, "3a", "Clear")),
        new(A, "3a IL", Heart + " Clear", "3a", "3a", "IL Heart Clear", "Start", new(SheetLabels.TabASides, "3a", Heart + " Clear")),
        new(A, "3a IL", Tape + " RTM", "3a", "3a", "IL Tape RTM", "Start", new(SheetLabels.TabASides, "3a", Tape + " RTM")),
        new(A, "3a IL", Heart + "+" + Tape + " Clear", "3a", "3a", "IL Heart Tape Clear", "Start", new(SheetLabels.TabASides, "3a", Heart + "+" + Tape + " Clear")),
        new(A, "3a IL", Heart + "+" + Tape + " RTM", "3a", "3a", "IL Heart Tape RTM", "Start", new(SheetLabels.TabASides, "3a", Heart + "+" + Tape + " RTM")),
        new(A, "4a CP", "4a Start", "4a", "4a", "Start", "Start"),
        new(A, "4a CP", "4a Start " + Tape + " Clear", "4a", "4a", "Start Tape Clear", "Start"),
        new(A, "4a CP", "4a Start " + Tape + " RTM", "4a", "4a", "Start Tape RTM", "Start"),
        new(A, "4a CP", "Shrine", "4a", "4a", "Shrine", "Shrine"),
        new(A, "4a CP", "Shrine " + Heart + " Clear", "4a", "4a", "Shrine Heart Clear", "Shrine"),
        new(A, "4a CP", "Shrine " + Heart + " RTM", "4a", "4a", "Shrine Heart RTM", "Shrine"),
        new(A, "4a CP", "Old Trail", "4a", "4a", "Old Trail", "Old Trail"),
        new(A, "4a CP", "Cliff Face", "4a", "4a", "Cliff Face", "Cliff Face"),
        new(A, "4a IL", "Clear", "4a", "4a", "IL", "Start", new(SheetLabels.TabASides, "4a", "Clear")),
        new(A, "4a IL", Heart + " Clear", "4a", "4a", "IL Heart Clear", "Start", new(SheetLabels.TabASides, "4a", Heart + " Clear")),
        new(A, "4a IL", Tape + " Clear", "4a", "4a", "IL Tape Clear", "Start", new(SheetLabels.TabASides, "4a", Tape + " Clear")),
        new(A, "4a IL", Heart + "+" + Tape + " Clear", "4a", "4a", "IL Heart Tape Clear", "Start", new(SheetLabels.TabASides, "4a", Heart + "+" + Tape + " Clear")),
        new(A, "4a IL", Heart + "+" + Tape + " RTM", "4a", "4a", "IL Heart Tape RTM", "Start", new(SheetLabels.TabASides, "4a", Heart + "+" + Tape + " RTM")),
        new(A, "5a CP", "5a Start", "5a", "5a", "Start", "Start"),
        new(A, "5a CP", "Depths", "5a", "5a", "Depths", "Depths"),
        new(A, "5a CP", "Depths " + Tape + " RTM", "5a", "5a", "Depths Tape RTM", "Depths"),
        new(A, "5a CP", "Depths " + Heart + "+" + Tape + " RTM", "5a", "5a", "Depths Heart Tape RTM", "Depths"),
        new(A, "5a CP", "Unravelling", "5a", "5a", "Unravelling", "Unravelling"),
        new(A, "5a CP", "Search", "5a", "5a", "Search", "Search"),
        new(A, "5a CP", "Rescue", "5a", "5a", "Rescue", "Rescue"),
        new(A, "5a IL", "Clear", "5a", "5a", "IL", "Start", new(SheetLabels.TabASides, "5a", "Clear")),
        new(A, "5a IL", Tape + " RTM", "5a", "5a", "IL Tape RTM", "Start", new(SheetLabels.TabASides, "5a", Tape + " RTM")),
        new(A, "5a IL", Heart + "+" + Tape + " RTM", "5a", "5a", "IL Heart Tape RTM", "Start", new(SheetLabels.TabASides, "5a", Heart + "+" + Tape + " RTM")),
        new(A, "6a CP", "6a Start", "6a", "6a", "Start", "Start"),
        new(A, "6a CP", "Lake", "6a", "6a", "Lake", "Lake"),
        new(A, "6a CP", "Hollows", "6a", "6a", "Hollows", "Hollows"),
        new(A, "6a CP", "Hollows " + Tape + " Clear", "6a", "6a", "Hollows Tape Clear", "Hollows"),
        new(A, "6a CP", "Hollows " + Tape + " RTM", "6a", "6a", "Hollows Tape RTM", "Hollows"),
        new(A, "6a CP", "Hollows " + Heart + "+" + Tape + " RTM", "6a", "6a", "Hollows Heart Tape RTM", "Hollows"),
        new(A, "6a CP", "Reflection", "6a", "6a", "Reflection", "Reflection"),
        new(A, "6a CP", "Rock Bottom", "6a", "6a", "Rock Bottom", "Rock Bottom"),
        new(A, "6a CP", "Resolution", "6a", "6a", "Resolution", "Resolution"),
        new(A, "6a IL", "Clear", "6a", "6a", "IL", "Start", new(SheetLabels.TabASides, "6a", "Clear")),
        new(A, "6a IL", Tape + " Clear", "6a", "6a", "IL Tape Clear", "Start", new(SheetLabels.TabASides, "6a", Tape + " Clear")),
        new(A, "6a IL", Tape + " RTM", "6a", "6a", "IL Tape RTM", "Start", new(SheetLabels.TabASides, "6a", Tape + " RTM")),
        new(A, "6a IL", Heart + "+" + Tape + " RTM", "6a", "6a", "IL Heart Tape RTM", "Start", new(SheetLabels.TabASides, "6a", Heart + "+" + Tape + " RTM")),
        new(A, "7a CP", "7a Start", "7a", "7a", "Start", "Start"),
        new(A, "7a CP", "500m", "7a", "7a", "500m", "500 M"),
        new(A, "7a CP", "1000m", "7a", "7a", "1000m", "1000 M"),
        new(A, "7a CP", "1500m", "7a", "7a", "1500m", "1500 M"),
        new(A, "7a CP", "1500m " + Tape + " Clear", "7a", "7a", "1500m Tape Clear", "1500 M"),
        new(A, "7a CP", "1500m " + Tape + " RTM", "7a", "7a", "1500m Tape RTM", "1500 M"),
        new(A, "7a CP", "2000m", "7a", "7a", "2000m", "2000 M"),
        new(A, "7a CP", "2500m", "7a", "7a", "2500m", "2500 M"),
        new(A, "7a CP", "3000m", "7a", "7a", "3000m", "3000 M"),
        new(A, "7a CP", "3000m " + Heart + " RTM", "7a", "7a", "3000m Heart RTM", "3000 M"),
        // 3000m's three pieces, virtual checkpoints (SegmentAutoDetect.SplitCheckpoints)
        new(A, "7a CP", "DownDraft", "7a", "7a", "Downdraft", "Downdraft", new(SheetLabels.TabASides, "7a", "Downdraft")),
        new(A, "7a CP", "UpDraft", "7a", "7a", "Updraft", "Updraft", new(SheetLabels.TabASides, "7a", "Updraft")),
        new(A, "7a CP", "NoDraft", "7a", "7a", "Nodraft", "Nodraft", new(SheetLabels.TabASides, "7a", "Nodraft")),
        new(A, "7a IL", "Clear", "7a", "7a", "IL", "Start", new(SheetLabels.TabASides, "7a", "Clear")),
        new(A, "7a IL", Tape + " Clear", "7a", "7a", "IL Tape Clear", "Start", new(SheetLabels.TabASides, "7a", Tape + " Clear")),
        new(A, "7a IL", Tape + " RTM", "7a", "7a", "IL Tape RTM", "Start", new(SheetLabels.TabASides, "7a", Tape + " RTM")),
        new(A, "7a IL", Heart + "+" + Tape + " RTM", "7a", "7a", "IL Heart Tape RTM", "Start", new(SheetLabels.TabASides, "7a", Heart + "+" + Tape + " RTM")),
        new(A, "8a CP", "8a Start", "8a", "8a", "Start", "Start"),
        new(A, "8a CP", "Into the Core", "8a", "8a", "Into the Core", "Into the Core"),
        new(A, "8a CP", "Hot and Cold", "8a", "8a", "Hot and Cold", "Hot and Cold"),
        // the sheet cuts the game's one "Heart of the Mountain" checkpoint in two;
        // the second half is virtual (SegmentAutoDetect.SplitCheckpoints)
        new(A, "8a CP", "HotM Vertical", "8a", "8a", "HotM Vertical", "Heart of the Mountain"),
        new(A, "8a CP", "HotM Horizontal", "8a", "8a", "HotM Horizontal", "HotM Horizontal"),
        new(A, "8a IL", "Clear", "8a", "8a", "IL", "Start", new(SheetLabels.TabASides, "8a", "Clear")),
        new(A, "8a IL", Tape + " Clear", "8a", "8a", "IL Tape Clear", "Start", new(SheetLabels.TabASides, "8a", Tape + " Clear")),
        // a B-side's "Clear" is its whole chapter: the tab has no IL block
        new(B, "1b", "1b Start", "1b", "1b", "Start", "Start"),
        new(B, "1b", "Contraption", "1b", "1b", "Contraption", "Contraption"),
        new(B, "1b", "Scrap Pit", "1b", "1b", "Scrap Pit", "Scrap Pit"),
        new(B, "1b", "1b Clear", "1b", "1b", "IL", "Start"),
        new(B, "2b", "2b Start", "2b", "2b", "Start", "Start"),
        new(B, "2b", "Combination Lock", "2b", "2b", "Combination Lock", "Combination Lock"),
        new(B, "2b", "Dream Altar", "2b", "2b", "Dream Altar", "Dream Altar"),
        new(B, "2b", "2b Clear", "2b", "2b", "IL", "Start"),
        new(B, "3b", "3b Start", "3b", "3b", "Start", "Start"),
        new(B, "3b", "Staff Quarters", "3b", "3b", "Staff Quarters", "Staff Quarters"),
        new(B, "3b", "Library", "3b", "3b", "Library", "Library"),
        new(B, "3b", "Rooftop", "3b", "3b", "Rooftop", "Rooftop"),
        new(B, "3b", "3b Clear", "3b", "3b", "IL", "Start"),
        new(B, "4b", "4b Start", "4b", "4b", "Start", "Start"),
        new(B, "4b", "Stepping Stones", "4b", "4b", "Stepping Stones", "Stepping Stones"),
        new(B, "4b", "Gusty Canyon", "4b", "4b", "Gusty Canyon", "Gusty Canyon"),
        new(B, "4b", "Eye of the Storm", "4b", "4b", "Eye of the Storm", "Eye of the Storm"),
        new(B, "4b", "4b Clear", "4b", "4b", "IL", "Start"),
        new(B, "5b", "5b Start", "5b", "5b", "Start", "Start"),
        new(B, "5b", "Central Chamber", "5b", "5b", "Central Chamber", "Central Chamber"),
        new(B, "5b", "Through the Mirror", "5b", "5b", "Through the Mirror", "Through the Mirror"),
        new(B, "5b", "Mix Master", "5b", "5b", "Mix Master", "Mix Master"),
        new(B, "5b", "5b Clear", "5b", "5b", "IL", "Start"),
        new(B, "6b", "6b Start", "6b", "6b", "Start", "Start"),
        // the sheet's name for 6B's Reflection
        new(B, "6b", "Falling", "6b", "6b", "Falling", "Reflection"),
        new(B, "6b", "Rock Bottom", "6b", "6b", "Rock Bottom", "Rock Bottom"),
        new(B, "6b", "Reprieve", "6b", "6b", "Reprieve", "Reprieve"),
        new(B, "6b", "6b Clear", "6b", "6b", "IL", "Start"),
        new(B, "7b", "7b Start", "7b", "7b", "Start", "Start"),
        new(B, "7b", "500m", "7b", "7b", "500m", "500 M"),
        new(B, "7b", "1000m", "7b", "7b", "1000m", "1000 M"),
        new(B, "7b", "1500m", "7b", "7b", "1500m", "1500 M"),
        new(B, "7b", "2000m", "7b", "7b", "2000m", "2000 M"),
        new(B, "7b", "2500m", "7b", "7b", "2500m", "2500 M"),
        new(B, "7b", "3000m", "7b", "7b", "3000m", "3000 M"),
        new(B, "7b", "7b Clear", "7b", "7b", "IL", "Start"),
        new(B, "8b", "8b Start", "8b", "8b", "Start", "Start"),
        new(B, "8b", "Into the Core", "8b", "8b", "Into the Core", "Into the Core"),
        new(B, "8b", "Burning or Freezing", "8b", "8b", "Burning or Freezing", "Burning or Freezing"),
        new(B, "8b", "Heartbeat", "8b", "8b", "Heartbeat", "Heartbeat"),
        new(B, "8b", "8b Clear", "8b", "8b", "IL", "Start"),
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
        // the berry tab: a row is its A-side row plus red berries, named
        // "ARB ..." under the A-side chapter. What a label does not say is in
        // RowTraits. The rows whose route returns to the map are left out
        new(R, "1a " + Berry, "Crossing to Heart", "1a", "1a", "ARB Crossing to Heart", "Crossing"),
        new(R, "1a " + Berry, "Start to Heart", "1a", "1a", "ARB Start to Heart", "Start"),
        new(R, "1a " + Berry, "Start", "1a", "1a", "ARB Start", "Start"),
        new(R, "1a " + Berry, "Crossing", "1a", "1a", "ARB Crossing", "Crossing"),
        new(R, "1a " + Berry, "Chasm", "1a", "1a", "ARB Chasm", "Chasm"),
        new(R, "2a " + Berry, "2a Start", "2a", "2a", "ARB Start", "Start"),
        new(R, "2a " + Berry, "Intervention", "2a", "2a", "ARB Intervention", "Intervention"),
        new(R, "2a " + Berry, "Awake", "2a", "2a", "ARB Awake", "Awake"),
        new(R, "3a " + Berry, "3a Start", "3a", "3a", "ARB Start", "Start"),
        new(R, "3a " + Berry, "Huge Mess", "3a", "3a", "ARB Huge Mess", "Huge Mess"),
        new(R, "3a " + Berry, "Elevator Shaft", "3a", "3a", "ARB Elevator Shaft", "Elevator Shaft"),
        new(R, "3a " + Berry, "Presidential Suite", "3a", "3a", "ARB Presidential Suite", "Presidential Suite"),
        new(R, "4a " + Berry, "4a Start", "4a", "4a", "ARB Start", "Start"),
        new(R, "4a " + Berry, "Shrine", "4a", "4a", "ARB Shrine", "Shrine"),
        new(R, "4a " + Berry, "Old Trail", "4a", "4a", "ARB Old Trail", "Old Trail"),
        new(R, "4a " + Berry, "Cliff Face (from RTM)", "4a", "4a", "ARB Cliff Face (from RTM)", "Cliff Face"),
        new(R, "4a " + Berry, "Cliff Face", "4a", "4a", "ARB Cliff Face", "Cliff Face"),
        new(R, "5a " + Berry, "Depths", "5a", "5a", "ARB Depths", "Depths"),
        new(R, "5a " + Berry, "Unravelling", "5a", "5a", "ARB Unravelling", "Unravelling"),
        new(R, "5a " + Berry, "Search", "5a", "5a", "ARB Search", "Search"),
        new(R, "5a " + Berry, "Rescue", "5a", "5a", "ARB Rescue", "Rescue"),
        new(R, "7a " + Berry, "7a Start", "7a", "7a", "ARB Start", "Start"),
        new(R, "7a " + Berry, "500m", "7a", "7a", "ARB 500m", "500 M"),
        new(R, "7a " + Berry, "1000m", "7a", "7a", "ARB 1000m", "1000 M"),
        new(R, "7a " + Berry, "1500m", "7a", "7a", "ARB 1500m", "1500 M"),
        new(R, "7a " + Berry, "2000m", "7a", "7a", "ARB 2000m", "2000 M"),
        new(R, "7a " + Berry, "2500m-full", "7a", "7a", "ARB 2500m-full", "2500 M"),
        new(R, "7a " + Berry, "3000m", "7a", "7a", "ARB 3000m", "3000 M"),
        new(R, "7a " + Berry, "Downdraft", "7a", "7a", "ARB Downdraft", "Downdraft"),
        new(R, "7a " + Berry, "Updraft", "7a", "7a", "ARB Updraft", "Updraft"),
        new(R, "7a " + Berry, "Nodraft", "7a", "7a", "ARB Nodraft", "Nodraft"),
        new(R, "8a " + Berry, "Into the Core", "8a", "8a", "ARB Into the Core", "Into the Core"),
        new(R, "8a " + Berry, "Hot and Cold", "8a", "8a", "ARB Hot and Cold", "Hot and Cold"),
        new(R, "8a " + Berry, "HotM Vertical", "8a", "8a", "ARB HotM Vertical", "Heart of the Mountain"),
        // the tab's second block has no Checkpoint column: a chapter's row is
        // named by its chapter cell, and is the whole chapter with every berry
        new(R, "2a " + Berry, "2a " + Berry, "2a", "2a", "ARB IL", "Start"),
        new(R, "3a " + Berry, "3a " + Berry, "3a", "3a", "ARB IL", "Start"),
        new(R, "4a " + Berry, "4a " + Berry, "4a", "4a", "ARB IL", "Start"),
        new(R, "7a " + Berry, "7a " + Berry, "7a", "7a", "ARB IL", "Start"),
        new(R, "8a " + Berry, "8a " + Berry, "8a", "8a", "ARB IL", "Start"),
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

    /// Same label on the matching entry tab, under the Standards chapter cell
    /// less a trailing " CP" ("1a CP" -> "1a") and otherwise as it stands (the
    /// berry tab's cell is the entry tab's); the Farewell tab has no chapter column.
    internal static SheetRowRef DefaultTarget(SheetRow row) {
        string chapter = row.Tab == StandardsTab.Farewell ? ""
            : row.SheetChapter.EndsWith(" CP", System.StringComparison.Ordinal) ? row.SheetChapter[..^3]
            : row.SheetChapter;
        return new SheetRowRef(StandardsTabs.Of(row.Tab).EntryTab, chapter, row.Label);
    }
}
