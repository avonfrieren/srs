using System.Collections.Generic;
using Celeste.Mod.SpeedrunTool;

namespace Celeste.Mod.SpeedrunSheet;

// Rooms per checkpoint of the any% route (community data), keyed by the
// normalized (chapter, checkpoint) names of the sheet. SpeedrunTool's room
// timer completes once its "Number of Rooms" setting is passed, so selecting
// a checkpoint pushes its room count there: a run started from the beginning
// of the checkpoint then completes exactly on its last room, which is what
// TierComparison compares against the sheet.
public static class RoomCounts {
    // checkpoints with no known room count (the "?" finals of each chapter,
    // Granny, Hollows Tape): SpeedrunTool's own slider maximum keeps the room
    // count from ever completing the timer, so the run ends through what
    // actually ends it in game (chapter completion, cassette, summit flag)
    public const int Unknown = 99;

    private static readonly Dictionary<(string Chapter, string Checkpoint), int> Counts = new() {
        // Prologue: Granny unknown (single segment, ends with the chapter)
        [("1a", "Start")] = 6,
        [("1a", "Crossing")] = 8,
        // Chasm unknown
        [("2a", "Start")] = 12,
        [("2a", "Intervention")] = 13,
        // Awake unknown
        [("3a", "Start")] = 20,
        [("3a", "Huge Mess")] = 26,
        [("3a", "Elevator Shaft")] = 5,
        // Presidential Suite unknown
        [("4a", "Start")] = 11,
        [("4a", "Shrine")] = 5,
        [("4a", "Old Trail")] = 8,
        // Cliff Face unknown
        [("5a/b", "5a Start")] = 10,
        [("5a/b", "Depths")] = 9,
        // full-5a checkpoints absent from the current tab (5B route only):
        // Unravelling = 12, Search = 8, Rescue unknown
        [("5a/b", "5b Start")] = 4,
        [("5a/b", "Central Chamber")] = 8,
        [("5a/b", "Through The Mirror")] = 5,
        // Mix Master unknown
        [("6a/b", "6a Start")] = 1,
        [("6a/b", "Lake")] = 6,
        [("6a/b", "Hollows")] = 17,
        [("6a/b", "Reflection")] = 5,
        [("6a/b", "6a Rock Bottom")] = 21,
        // Resolution unknown; Hollows Tape unknown (ends with the cassette)
        [("6a/b", "6b Start")] = 7,
        [("6a/b", "Falling")] = 10, // the game's 6B Reflection, renamed by the sheet
        [("6a/b", "6b Rock Bottom")] = 5,
        // Reprieve unknown
        [("7a", "Start")] = 7,
        [("7a", "500m")] = 9,
        [("7a", "1000m")] = 9,
        [("7a", "1500m")] = 11,
        [("7a", "2000m")] = 12,
        [("7a", "2500m")] = 12,
        // 3000m unknown
    };

    public static int TargetFor(SheetSegment segment) =>
        Counts.TryGetValue((segment.Chapter, segment.Name), out int rooms) ? rooms : Unknown;

    public static void Apply(SheetSegment segment) {
        if (segment != null && SpeedrunToolSettings.Instance is { } settings) {
            settings.NumberOfRooms = TargetFor(segment);
        }
    }
}
