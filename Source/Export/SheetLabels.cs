using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// A writable row of the personal practice sheet, identified by its tab and its
/// two label columns. Chapter is empty on the Farewell tab, which has none.
public readonly record struct SheetRowRef(string Tab, string Chapter, string Cp);

/// Frozen translation from srs's own (chapter, checkpoint) naming to the row it
/// is written to in the player's sheet. Hardcoded, with no normalisation: rows
/// absent from this table are never exported.
public static class SheetLabels {
    public const string TabASides = "A Sides";
    public const string TabBCSides = "B+C Sides";
    public const string TabFarewell = "Farewell";

    // kept as escaped code points so this source stays ASCII
    private const string Heart = "\U0001F499"; // blue heart
    private const string Tape = "\U0001F4FC";  // cassette

    private static readonly Dictionary<(string Chapter, string Name), SheetRowRef> map = new() {
        [("Prologue", "Granny")] = new(TabASides, "Prologue", "Granny"),

        [("1a", "Start")] = new(TabASides, "1a", "1a Start"),
        [("1a", "Crossing")] = new(TabASides, "1a", "Crossing"),
        [("1a", "Chasm")] = new(TabASides, "1a", "Chasm"),

        [("2a", "Start")] = new(TabASides, "2a", "2a Start"),
        [("2a", "Start Heart RC")] = new(TabASides, "2a", "2a Start " + Heart + " RC"),
        [("2a", "Intervention")] = new(TabASides, "2a", "Intervention"),
        [("2a", "Awake")] = new(TabASides, "2a", "Awake"),

        [("3a", "Start")] = new(TabASides, "3a", "3a Start"),
        [("3a", "Huge Mess")] = new(TabASides, "3a", "Huge Mess"),
        [("3a", "Huge Mess Heart")] = new(TabASides, "3a", "Huge Mess " + Heart),
        [("3a", "Elevator Shaft")] = new(TabASides, "3a", "Elevator Shaft"),
        [("3a", "Presidential Suite")] = new(TabASides, "3a", "Presidential Suite"),

        [("4a", "Start")] = new(TabASides, "4a", "4a Start"),
        [("4a", "Shrine")] = new(TabASides, "4a", "Shrine"),
        [("4a", "Shrine Heart")] = new(TabASides, "4a", "Shrine " + Heart + " Clear"),
        [("4a", "Old Trail")] = new(TabASides, "4a", "Old Trail"),
        [("4a", "Cliff Face")] = new(TabASides, "4a", "Cliff Face"),

        // the folded "5a/b" chapter splits back into 5a on one tab and 5b on the other
        [("5a/b", "5a Start")] = new(TabASides, "5a", "5a Start"),
        [("5a/b", "Depths")] = new(TabASides, "5a", "Depths"),
        [("5a/b", "Depths Tape")] = new(TabASides, "5a", "Depths " + Tape + " RTM"),
        [("5a/b", "Unravelling")] = new(TabASides, "5a", "Unravelling"),
        [("5a/b", "Search")] = new(TabASides, "5a", "Search"),
        [("5a/b", "Rescue")] = new(TabASides, "5a", "Rescue"),
        [("5a/b", "5b Start")] = new(TabBCSides, "5b", "5b Start"),
        [("5a/b", "Central Chamber")] = new(TabBCSides, "5b", "Central Chamber"),
        [("5a/b", "Through the Mirror")] = new(TabBCSides, "5b", "Through the Mirror"),
        [("5a/b", "Mix Master")] = new(TabBCSides, "5b", "Mix Master"),

        // same split for "6a/b"; the side prefix on Rock Bottom is srs's own,
        // the sheet tells the two apart by their chapter
        [("6a/b", "6a Start")] = new(TabASides, "6a", "6a Start"),
        [("6a/b", "Lake")] = new(TabASides, "6a", "Lake"),
        [("6a/b", "Hollows")] = new(TabASides, "6a", "Hollows"),
        [("6a/b", "Hollows Tape")] = new(TabASides, "6a", "Hollows " + Tape + " RTM"),
        [("6a/b", "Reflection")] = new(TabASides, "6a", "Reflection"),
        [("6a/b", "6a Rock Bottom")] = new(TabASides, "6a", "Rock Bottom"),
        [("6a/b", "Resolution")] = new(TabASides, "6a", "Resolution"),
        [("6a/b", "6b Start")] = new(TabBCSides, "6b", "6b Start"),
        [("6a/b", "Falling")] = new(TabBCSides, "6b", "Falling"),
        [("6a/b", "6b Rock Bottom")] = new(TabBCSides, "6b", "Rock Bottom"),
        [("6a/b", "Reprieve")] = new(TabBCSides, "6b", "Reprieve"),

        [("7a", "7a Start")] = new(TabASides, "7a", "7a Start"),
        [("7a", "500m")] = new(TabASides, "7a", "500m"),
        [("7a", "1000m")] = new(TabASides, "7a", "1000m"),
        [("7a", "1500m")] = new(TabASides, "7a", "1500m"),
        [("7a", "2000m")] = new(TabASides, "7a", "2000m"),
        [("7a", "2500m")] = new(TabASides, "7a", "2500m"),
        [("7a", "3000m")] = new(TabASides, "7a", "3000m"),

        [("8a", "Start")] = new(TabASides, "8a", "8a Start"),
        [("8a", "Into the Core")] = new(TabASides, "8a", "Into the Core"),
        [("8a", "Hot and Cold")] = new(TabASides, "8a", "Hot and Cold"),
        [("8a", "HotM Vertical")] = new(TabASides, "8a", "HotM Vertical"),
        [("8a", "HotM Horizontal")] = new(TabASides, "8a", "HotM Horizontal"),

        // the Farewell tab has no chapter column
        [("Farewell", "Start")] = new(TabFarewell, "", "Start"),
        [("Farewell", "Singular")] = new(TabFarewell, "", "Singular"),
        [("Farewell", "Power Source")] = new(TabFarewell, "", "Power Source"),
        [("Farewell", "Remembered")] = new(TabFarewell, "", "Remembered"),
        [("Farewell", "Event Horizon")] = new(TabFarewell, "", "Event Horizon"),
        [("Farewell", "Determination")] = new(TabFarewell, "", "Determination"),
        [("Farewell", "Start DTS")] = new(TabFarewell, "", "Start DTS"),
        [("Farewell", "Singular DTS")] = new(TabFarewell, "", "Singular DTS"),
        [("Farewell", "Power Source DTS")] = new(TabFarewell, "", "Power Source DTS"),
        [("Farewell", "Remembered DTS")] = new(TabFarewell, "", "Remembered DTS"),
        [("Farewell", "Event Horizon DTS")] = new(TabFarewell, "", "Event Horizon DTS"),
        [("Farewell", "Determination DTS")] = new(TabFarewell, "", "Determination DTS"),
        [("Farewell", "Stubbornness")] = new(TabFarewell, "", "Stubbornness"),
        [("Farewell", "Reconciliation")] = new(TabFarewell, "", "Reconciliation"),
        [("Farewell", "Farewell")] = new(TabFarewell, "", "Farewell"),
    };

    /// internal so the tests can check the table against SheetData.Import
    internal static IReadOnlyDictionary<(string Chapter, string Name), SheetRowRef> Map => map;

    public static bool TryMap(string srsChapter, string srsName, out SheetRowRef row) =>
        map.TryGetValue((srsChapter, srsName), out row);
}
