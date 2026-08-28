using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// A writable row of the personal practice sheet, identified by its tab and its
/// two label columns. Chapter is empty on the Farewell tab, which has none.
public readonly record struct SheetRowRef(string Tab, string Chapter, string Cp);

/// Frozen translation from srs's own (chapter, checkpoint) naming to the row it
/// is written to in the player's sheet. Hardcoded, with no normalisation: rows
/// absent from this table are never exported.
public static class SheetLabels {
    public const string TAB_A_SIDES = "A Sides";
    public const string TAB_B_C_SIDES = "B+C Sides";
    public const string TAB_FAREWELL = "Farewell";

    // kept as escaped code points so this source stays ASCII
    private const string HEART = "\U0001F499"; // blue heart
    private const string TAPE = "\U0001F4FC";  // cassette

    private static readonly Dictionary<(string Chapter, string Name), SheetRowRef> _map = new() {
        [("Prologue", "Granny")] = new(TAB_A_SIDES, "Prologue", "Granny"),

        [("1a", "Start")] = new(TAB_A_SIDES, "1a", "1a Start"),
        [("1a", "Crossing")] = new(TAB_A_SIDES, "1a", "Crossing"),
        [("1a", "Chasm")] = new(TAB_A_SIDES, "1a", "Chasm"),

        [("2a", "Start")] = new(TAB_A_SIDES, "2a", "2a Start"),
        [("2a", "Start Heart")] = new(TAB_A_SIDES, "2a", "2a Start " + HEART + " RC"),
        [("2a", "Intervention")] = new(TAB_A_SIDES, "2a", "Intervention"),
        [("2a", "Awake")] = new(TAB_A_SIDES, "2a", "Awake"),

        [("3a", "Start")] = new(TAB_A_SIDES, "3a", "3a Start"),
        [("3a", "Huge Mess")] = new(TAB_A_SIDES, "3a", "Huge Mess"),
        [("3a", "Huge Mess Heart")] = new(TAB_A_SIDES, "3a", "Huge Mess " + HEART),
        [("3a", "Elevator Shaft")] = new(TAB_A_SIDES, "3a", "Elevator Shaft"),
        [("3a", "Presidential Suite")] = new(TAB_A_SIDES, "3a", "Presidential Suite"),

        [("4a", "Start")] = new(TAB_A_SIDES, "4a", "4a Start"),
        [("4a", "Shrine")] = new(TAB_A_SIDES, "4a", "Shrine"),
        [("4a", "Shrine Heart")] = new(TAB_A_SIDES, "4a", "Shrine " + HEART + " Clear"),
        [("4a", "Old Trail")] = new(TAB_A_SIDES, "4a", "Old Trail"),
        [("4a", "Cliff Face")] = new(TAB_A_SIDES, "4a", "Cliff Face"),

        // the folded "5a/b" chapter splits back into 5a on one tab and 5b on the other
        [("5a/b", "5a Start")] = new(TAB_A_SIDES, "5a", "5a Start"),
        [("5a/b", "Depths")] = new(TAB_A_SIDES, "5a", "Depths"),
        [("5a/b", "Depths Tape")] = new(TAB_A_SIDES, "5a", "Depths " + TAPE + " RTM"),
        [("5a/b", "Unravelling")] = new(TAB_A_SIDES, "5a", "Unravelling"),
        [("5a/b", "Search")] = new(TAB_A_SIDES, "5a", "Search"),
        [("5a/b", "Rescue")] = new(TAB_A_SIDES, "5a", "Rescue"),
        [("5a/b", "5b Start")] = new(TAB_B_C_SIDES, "5b", "5b Start"),
        [("5a/b", "Central Chamber")] = new(TAB_B_C_SIDES, "5b", "Central Chamber"),
        [("5a/b", "Through the Mirror")] = new(TAB_B_C_SIDES, "5b", "Through the Mirror"),
        [("5a/b", "Mix Master")] = new(TAB_B_C_SIDES, "5b", "Mix Master"),

        // same split for "6a/b"; the side prefix on Rock Bottom is srs's own,
        // the sheet tells the two apart by their chapter
        [("6a/b", "6a Start")] = new(TAB_A_SIDES, "6a", "6a Start"),
        [("6a/b", "Lake")] = new(TAB_A_SIDES, "6a", "Lake"),
        [("6a/b", "Hollows")] = new(TAB_A_SIDES, "6a", "Hollows"),
        [("6a/b", "Hollows Tape")] = new(TAB_A_SIDES, "6a", "Hollows " + TAPE + " RTM"),
        [("6a/b", "Reflection")] = new(TAB_A_SIDES, "6a", "Reflection"),
        [("6a/b", "6a Rock Bottom")] = new(TAB_A_SIDES, "6a", "Rock Bottom"),
        [("6a/b", "Resolution")] = new(TAB_A_SIDES, "6a", "Resolution"),
        [("6a/b", "6b Start")] = new(TAB_B_C_SIDES, "6b", "6b Start"),
        [("6a/b", "Falling")] = new(TAB_B_C_SIDES, "6b", "Falling"),
        [("6a/b", "6b Rock Bottom")] = new(TAB_B_C_SIDES, "6b", "Rock Bottom"),
        [("6a/b", "Reprieve")] = new(TAB_B_C_SIDES, "6b", "Reprieve"),

        [("7a", "0m")] = new(TAB_A_SIDES, "7a", "0m"),
        [("7a", "500m")] = new(TAB_A_SIDES, "7a", "500m"),
        [("7a", "1000m")] = new(TAB_A_SIDES, "7a", "1000m"),
        [("7a", "1500m")] = new(TAB_A_SIDES, "7a", "1500m"),
        [("7a", "2000m")] = new(TAB_A_SIDES, "7a", "2000m"),
        [("7a", "2500m")] = new(TAB_A_SIDES, "7a", "2500m"),
        [("7a", "3000m")] = new(TAB_A_SIDES, "7a", "3000m"),

        [("8a", "Start")] = new(TAB_A_SIDES, "8a", "8a Start"),
        [("8a", "Into the Core")] = new(TAB_A_SIDES, "8a", "Into the Core"),
        [("8a", "Hot and Cold")] = new(TAB_A_SIDES, "8a", "Hot and Cold"),
        [("8a", "HotM Vertical")] = new(TAB_A_SIDES, "8a", "HotM Vertical"),
        [("8a", "HotM Horizontal")] = new(TAB_A_SIDES, "8a", "HotM Horizontal"),

        // the Farewell tab has no chapter column
        [("Farewell", "Start")] = new(TAB_FAREWELL, "", "Start"),
        [("Farewell", "Singular")] = new(TAB_FAREWELL, "", "Singular"),
        [("Farewell", "Power Source")] = new(TAB_FAREWELL, "", "Power Source"),
        [("Farewell", "Remembered")] = new(TAB_FAREWELL, "", "Remembered"),
        [("Farewell", "Event Horizon")] = new(TAB_FAREWELL, "", "Event Horizon"),
        [("Farewell", "Determination")] = new(TAB_FAREWELL, "", "Determination"),
        [("Farewell", "Start DTS")] = new(TAB_FAREWELL, "", "Start DTS"),
        [("Farewell", "Singular DTS")] = new(TAB_FAREWELL, "", "Singular DTS"),
        [("Farewell", "Power Source DTS")] = new(TAB_FAREWELL, "", "Power Source DTS"),
        [("Farewell", "Remembered DTS")] = new(TAB_FAREWELL, "", "Remembered DTS"),
        [("Farewell", "Event Horizon DTS")] = new(TAB_FAREWELL, "", "Event Horizon DTS"),
        [("Farewell", "Determination DTS")] = new(TAB_FAREWELL, "", "Determination DTS"),
        [("Farewell", "Stubbornness")] = new(TAB_FAREWELL, "", "Stubbornness"),
        [("Farewell", "Reconciliation")] = new(TAB_FAREWELL, "", "Reconciliation"),
        [("Farewell", "Farewell")] = new(TAB_FAREWELL, "", "Farewell"),
    };

    /// internal so the tests can check the table against SheetData.Import
    internal static IReadOnlyDictionary<(string Chapter, string Name), SheetRowRef> Map => _map;

    public static bool TryMap(string srsChapter, string srsName, out SheetRowRef row) =>
        _map.TryGetValue((srsChapter, srsName), out row);
}
