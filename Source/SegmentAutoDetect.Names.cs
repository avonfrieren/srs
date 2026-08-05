using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// The game-to-sheet checkpoint name table, split from the rest of
// SegmentAutoDetect (which needs Celeste and SpeedrunTool types) so the tests
// can check every name it points at still exists in the imported sheet.
public static partial class SegmentAutoDetect {
    // (side or chapter, game checkpoint name) -> sheet checkpoint name.
    // Deliberately a hardcoded table, no name normalization (owner decision
    // 2026-07-18, kept for the v2.0.0 sheet). "Start" stands for the
    // session's first room (which has no CheckpointData). Game checkpoints
    // not imported from the sheet (the full-5A route past Depths) are simply
    // not listed — reaching them leaves the selection where it was. The two
    // cassette checkpoints stay manual-only: "Hollows Tape" starts at 6A's
    // Hollows checkpoint and "Depths Tape" at 5A's Depths checkpoint,
    // indistinguishable from "Hollows"/"Depths"
    internal static readonly Dictionary<(string Scope, string GameName), string> CheckpointMap = new() {
        [("Prologue", "Start")] = "Granny",
        [("1a", "Start")] = "Start",
        [("1a", "Crossing")] = "Crossing",
        [("1a", "Chasm")] = "Chasm",
        [("2a", "Start")] = "Start",
        [("2a", "Intervention")] = "Intervention",
        [("2a", "Awake")] = "Awake",
        [("3a", "Start")] = "Start",
        [("3a", "Huge Mess")] = "Huge Mess",
        [("3a", "Elevator Shaft")] = "Elevator Shaft",
        [("3a", "Presidential Suite")] = "Presidential Suite",
        [("4a", "Start")] = "Start",
        [("4a", "Shrine")] = "Shrine",
        [("4a", "Old Trail")] = "Old Trail",
        [("4a", "Cliff Face")] = "Cliff Face",
        [("5a", "Start")] = "5a Start",
        [("5a", "Depths")] = "Depths",
        [("5b", "Start")] = "5b Start",
        [("5b", "Central Chamber")] = "Central Chamber",
        [("5b", "Through the Mirror")] = "Through The Mirror",
        [("5b", "Mix Master")] = "Mix Master",
        [("6a", "Start")] = "6a Start",
        [("6a", "Lake")] = "Lake",
        [("6a", "Hollows")] = "Hollows",
        [("6a", "Reflection")] = "Reflection",
        [("6a", "Rock Bottom")] = "6a Rock Bottom",
        [("6a", "Resolution")] = "Resolution",
        [("6b", "Start")] = "6b Start",
        [("6b", "Reflection")] = "Falling", // the sheet's name for 6B Reflection
        [("6b", "Rock Bottom")] = "6b Rock Bottom",
        [("6b", "Reprieve")] = "Reprieve",
        [("7a", "Start")] = "0m", // the new sheet's name for 7a Start
        [("7a", "500 M")] = "500m",
        [("7a", "1000 M")] = "1000m",
        [("7a", "1500 M")] = "1500m",
        [("7a", "2000 M")] = "2000m",
        [("7a", "2500 M")] = "2500m",
        [("7a", "3000 M")] = "3000m",
    };
}
