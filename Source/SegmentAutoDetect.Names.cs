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
    // cassette checkpoints start at the same in-game checkpoint as their
    // plain sibling ("Hollows Tape" at 6A's Hollows, "Depths Tape" at 5A's
    // Depths) — nothing observable tells them apart, so this table maps to
    // the plain name and the player's Category setting picks the variant
    // through CategoryVariants
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

    // (category, sheet name from CheckpointMap) -> the category's variant of
    // that checkpoint. The variant wins when the imported sheet has it; every
    // unlisted pair keeps the plain row, so a category with no variant data
    // yet still auto-detects the closest thing the sheet offers
    internal static readonly Dictionary<(SegmentCategory Category, string SheetName), string> CategoryVariants = new() {
        [(SegmentCategory.Cassette, "Depths")] = "Depths Tape",
        [(SegmentCategory.Cassette, "Hollows")] = "Hollows Tape",
    };

    // (scope, game checkpoint name) -> the room a run of that checkpoint's
    // segment really starts in, for the segments the sheet does not time from
    // the checkpoint's own room (v3.2.0). Keyed by *game* name, not sheet
    // name, so both ends of a segment read the same entry: a segment ends
    // exactly where the next one starts, so an override moves the previous
    // segment's finish line with it, and the two never overlap. Variants
    // inherit it like they inherit their anchor
    internal static readonly Dictionary<(string Scope, string GameName), string> StartRoomOverrides = new() {
        // the sheet times "Awake" from the moment Madeline wakes up, three
        // rooms before the game's Awake checkpoint: end_0 is the campfire
        // room right after the dream section, then end_1, end_2, and only
        // then end_3, which carries the checkpoint. Corollary: a run of
        // Intervention ends on entering end_0, not end_3
        [("2a", "Awake")] = "end_0",
        // 7A opens on a-00-intro, but neither that room nor Madeline's
        // landing animation in a-00 is timed — the sheet adds their time
        // afterwards, so they are none of this mod's business. Runs start
        // from a savestate placed after the landing with a Current Room
        // timer, which puts the start of the run in a-00
        [("7a", "Start")] = "a-00",
    };

    // StartRoomOverrides read backwards: the game checkpoint whose segment is
    // timed from this room, or null. This is what lets the auto-detection move
    // the selection when the override room is entered — three rooms before the
    // game checkpoint would otherwise still read as the previous segment
    internal static string OverriddenCheckpointAt(string scope, string room) {
        foreach (KeyValuePair<(string Scope, string GameName), string> entry in StartRoomOverrides) {
            if (entry.Key.Scope == scope && entry.Value == room) {
                return entry.Key.GameName;
            }
        }

        return null;
    }

    // variant -> plain sibling ("Depths Tape" -> "Depths"); plain names come
    // back unchanged. This is how a variant inherits its in-game anchor: both
    // start at the same checkpoint
    internal static string PlainNameOf(string sheetName) {
        foreach (KeyValuePair<(SegmentCategory Category, string SheetName), string> entry in CategoryVariants) {
            if (entry.Value == sheetName) {
                return entry.Key.SheetName;
            }
        }

        return sheetName;
    }

    // CheckpointMap read backwards: the game checkpoint a sheet segment starts
    // at, inside one scope (game names repeat across scopes — "Start" — so the
    // scope is required). Null when the sheet name is not anchored in this
    // scope. RunWatcher resolves rooms from this: the start room of the run,
    // and the end room of Checkpoint segments (the next checkpoint's room)
    internal static string GameNameOf(string scope, string sheetName) {
        string plain = PlainNameOf(sheetName);
        foreach (KeyValuePair<(string Scope, string GameName), string> entry in CheckpointMap) {
            if (entry.Key.Scope == scope && entry.Value == plain) {
                return entry.Key.GameName;
            }
        }

        return null;
    }
}
