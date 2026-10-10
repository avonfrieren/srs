using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// The scope of a session, plus the name tables in SegmentAutoDetect.Names.cs.
public static partial class SegmentAutoDetect {
    // vanilla (AreaKey.ID, side) -> sheet chapter
    private static readonly Dictionary<(int Id, AreaMode Mode), string> ChapterMap = new() {
        [(0, AreaMode.Normal)] = "Prologue",
        [(1, AreaMode.Normal)] = "1a",
        [(2, AreaMode.Normal)] = "2a",
        [(3, AreaMode.Normal)] = "3a",
        [(4, AreaMode.Normal)] = "4a",
        [(5, AreaMode.Normal)] = "5a",
        [(5, AreaMode.BSide)] = "5b",
        [(6, AreaMode.Normal)] = "6a",
        [(6, AreaMode.BSide)] = "6b",
        [(7, AreaMode.Normal)] = "7a",
        // vanilla numbering skips the Epilogue (area 8): Core is 9, Farewell
        // is 10. The sheet gives Farewell a tab of its own, and the mod a
        // chapter of its own — named after the tab rather than "9a"
        [(9, AreaMode.Normal)] = "8a",
        [(10, AreaMode.Normal)] = "Farewell",
        [(1, AreaMode.BSide)] = "1b",
        [(2, AreaMode.BSide)] = "2b",
        [(3, AreaMode.BSide)] = "3b",
        [(4, AreaMode.BSide)] = "4b",
        [(7, AreaMode.BSide)] = "7b",
        [(9, AreaMode.BSide)] = "8b",
        [(1, AreaMode.CSide)] = "1c",
        [(2, AreaMode.CSide)] = "2c",
        [(3, AreaMode.CSide)] = "3c",
        [(4, AreaMode.CSide)] = "4c",
        [(5, AreaMode.CSide)] = "5c",
        [(6, AreaMode.CSide)] = "6c",
        [(7, AreaMode.CSide)] = "7c",
        [(9, AreaMode.CSide)] = "8c",
    };

    // the side a chapter's name tables are keyed by. Null outside the
    // chapters the sheet covers
    internal static string ScopeOf(Session session) =>
        ChapterMap.GetValueOrDefault((session.Area.ID, session.Area.Mode));
}
