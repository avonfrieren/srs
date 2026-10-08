using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// The scope of a session, plus the name tables in SegmentAutoDetect.Names.cs.
public static partial class SegmentAutoDetect {
    // vanilla (AreaKey.ID, side) -> sheet chapter, plus the side name inside
    // the folded chapters (the sheet routes 5 as 5B only, 6 as both sides)
    private static readonly Dictionary<(int Id, AreaMode Mode), (string Chapter, string Side)> ChapterMap = new() {
        [(0, AreaMode.Normal)] = ("Prologue", null),
        [(1, AreaMode.Normal)] = ("1a", null),
        [(2, AreaMode.Normal)] = ("2a", null),
        [(3, AreaMode.Normal)] = ("3a", null),
        [(4, AreaMode.Normal)] = ("4a", null),
        [(5, AreaMode.Normal)] = ("5a/b", "5a"),
        [(5, AreaMode.BSide)] = ("5a/b", "5b"),
        [(6, AreaMode.Normal)] = ("6a/b", "6a"),
        [(6, AreaMode.BSide)] = ("6a/b", "6b"),
        [(7, AreaMode.Normal)] = ("7a", null),
        // vanilla numbering skips the Epilogue (area 8): Core is 9, Farewell
        // is 10. The sheet gives Farewell a tab of its own, and the mod a
        // chapter of its own — named after the tab rather than "9a"
        [(9, AreaMode.Normal)] = ("8a", null),
        [(10, AreaMode.Normal)] = ("Farewell", null),
        [(1, AreaMode.CSide)] = ("1c", null),
        [(2, AreaMode.CSide)] = ("2c", null),
        [(3, AreaMode.CSide)] = ("3c", null),
        [(4, AreaMode.CSide)] = ("4c", null),
        [(5, AreaMode.CSide)] = ("5c", null),
        [(6, AreaMode.CSide)] = ("6c", null),
        [(7, AreaMode.CSide)] = ("7c", null),
        [(9, AreaMode.CSide)] = ("8c", null),
    };

    // the scope a chapter's name tables are keyed by: the side for the folded
    // chapters (5a/b, 6a/b), the chapter itself otherwise. Null outside the
    // chapters the sheet covers
    internal static string ScopeOf(Session session) =>
        ChapterMap.TryGetValue((session.Area.ID, session.Area.Mode), out (string Chapter, string Side) chapter)
            ? chapter.Side ?? chapter.Chapter
            : null;
}
