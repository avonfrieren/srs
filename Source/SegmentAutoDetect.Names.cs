using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// The checkpoint name tables, split from the rest of SegmentAutoDetect (which
// needs Celeste types) so the tests can check them against the row table.
public static partial class SegmentAutoDetect {
    // (scope, game checkpoint) -> the virtual checkpoint the sheet inserts
    // after it: the game's one "Heart of the Mountain" is two segments on the
    // sheet. The second half exists only here and in SheetRows, anchored by its
    // StartRoomOverrides room, which both halves read. Only a virtual
    // checkpoint closing its chapter is supported: nothing resolves the end
    // room of one followed by a real checkpoint
    internal static readonly Dictionary<(string Scope, string GameName), string> SplitCheckpoints = new() {
        [("8a", "Heart of the Mountain")] = "HotM Horizontal",
    };

    // (scope, game checkpoint) -> the room a segment really starts in, where
    // the sheet does not time it from the checkpoint's own room. Keyed by game
    // name, so the segment before ends there too: the two never overlap
    internal static readonly Dictionary<(string Scope, string GameName), string> StartRoomOverrides = new() {
        // the sheet times Awake from the wake-up in end_0, three rooms before
        // the checkpoint's end_3: Intervention ends on entering end_0
        [("2a", "Awake")] = "end_0",
        // neither a-00-intro nor the landing in a-00 is timed: the sheet adds
        // them (UntimedSegmentHead)
        [("7a", "Start")] = "a-00",
        // HotM Horizontal has no checkpoint of its own: d-08 is where the chase
        // to the right begins
        [("8a", "HotM Horizontal")] = "d-08",
    };

    // (scope, game checkpoint) -> the sheet's own constant for the head of a
    // segment srs does not time, added to the reading so the tier is read off
    // the number the thresholds describe. 7A from a-00, after the intro room
    // and the landing; the Prologue as file time, which counts the 61 frames
    // of the intro before its savestate
    internal static readonly Dictionary<(string Scope, string GameName), TimeSpan> UntimedSegmentHead = new() {
        [("7a", "Start")] = new TimeSpan(0, 0, 0, 5, 508),
        [("Prologue", "Start")] = new TimeSpan(0, 0, 0, 1, 37),
    };

    // (scope, game checkpoint) -> the end of a segment the timer does not
    // count, added like a head: the Prologue's file time runs 33 frames past
    // the timer's last tick, to the LevelExit, skipped or watched
    internal static readonly Dictionary<(string Scope, string GameName), TimeSpan> UntimedSegmentTail = new() {
        [("Prologue", "Start")] = new TimeSpan(0, 0, 0, 0, 561),
    };

    // (scope, game checkpoint) of the segments that open once the launch into
    // their room is over: 7A's head already counts the landing in a-00, and
    // opening on the entry would count it twice
    internal static readonly HashSet<(string Scope, string GameName)> AfterLaunchStarts = [
        ("7a", "Start"),
    ];

    // (scope, game checkpoint) of the segments valid from their start point,
    // beyond every chapter's "Start": the segments after a wake-up (owner)
    internal static readonly HashSet<(string Scope, string GameName)> CurrentRoomStarts = [
        ("2a", "Awake"),
        ("5a", "Unravelling"),
        ("5b", "Through the Mirror"),
    ];

    // (scope, game checkpoint) -> the room a segment's first room must be
    // entered from for it to open, and for the one before to close with a
    // time. Every Next Room segment and every segment after a wake-up has one;
    // a chapter's Start and 7A's start have none. A room reached by a cutscene
    // is entered from the room the cutscene starts in
    internal static readonly Dictionary<(string Scope, string GameName), string> EntryRooms = new() {
        [("1a", "Crossing")] = "5",
        [("1a", "Chasm")] = "9",
        [("2a", "Intervention")] = "3x",
        [("2a", "Awake")] = "13",
        [("3a", "Huge Mess")] = "07-a",
        [("3a", "Elevator Shaft")] = "09-b",
        [("3a", "Presidential Suite")] = "02-d",
        [("4a", "Shrine")] = "a-09",
        [("4a", "Old Trail")] = "b-08",
        // d-00 is also entered from c-10, the berry room beside c-08: only the
        // berry routes, which are not imported, go that way (owner)
        [("4a", "Cliff Face")] = "c-08",
        [("5a", "Depths")] = "a-13",
        [("5a", "Unravelling")] = "void",
        [("5a", "Search")] = "c-13",
        // e-00 is also reached up the shaft of d-01, which no route takes (owner)
        [("5a", "Rescue")] = "d-20",
        [("5b", "Central Chamber")] = "a-02",
        [("5b", "Through the Mirror")] = "b-09",
        [("5b", "Mix Master")] = "c-04",
        // 00 shares an edge only with a room of its own segment: it is reached
        // by the fall from start, watched or skipped
        [("6a", "Lake")] = "start",
        [("6a", "Hollows")] = "02b",
        [("6a", "Reflection")] = "20",
        [("6a", "Rock Bottom")] = "b-03",
        [("6a", "Resolution")] = "boss-20",
        [("6b", "Reflection")] = "a-06",
        [("6b", "Rock Bottom")] = "b-10",
        [("6b", "Reprieve")] = "c-04",
        [("7a", "500 M")] = "a-06",
        [("7a", "1000 M")] = "b-09",
        [("7a", "1500 M")] = "c-09",
        [("7a", "2000 M")] = "d-11",
        [("7a", "2500 M")] = "e-13",
        [("7a", "3000 M")] = "f-11",
        [("8a", "Into the Core")] = "02",
        [("8a", "Hot and Cold")] = "b-07",
        [("8a", "Heart of the Mountain")] = "c-04",
        [("8a", "HotM Horizontal")] = "d-07",
        [("Farewell", "Singular")] = "intro-03-space",
        [("Farewell", "Power Source")] = "b-07",
        [("Farewell", "Remembered")] = "e-00y",
        [("Farewell", "Event Horizon")] = "e-08",
        [("Farewell", "Determination")] = "g-06",
        [("Farewell", "Stubbornness")] = "h-10",
        [("Farewell", "Reconciliation")] = "i-05",
        [("Farewell", "Farewell")] = "j-15",
    };

    // (scope, game checkpoint) -> the spawn a wake-up puts the player on, in
    // world coordinates: the only spawn a Current Room start that is not a
    // "Start" opens from. Not the default spawn: in 5A's and 5B's c-00 that is
    // the bottom one, and the wake-up uses the top one. One missing here never
    // opens on a restart
    internal static readonly Dictionary<(string Scope, string GameName), (int X, int Y)> WakeUpSpawns = new() {
        [("2a", "Awake")] = (144, 1880),
        [("5a", "Unravelling")] = (-832, 1688),
        [("5b", "Through the Mirror")] = (3680, -984),
    };

    // (scope, game checkpoint) -> where the player stands, relative to a spawn
    // of the start room, on the first frame with control; every other Current
    // Room start measured is on the spawn
    internal static readonly Dictionary<(string Scope, string GameName), (int X, int Y)> SpawnOffsets = new() {
        [("Farewell", "Start")] = (8, 0),
        // the campfire cutscene ends with the player at the bonfire, watched
        // or skipped (CS06_Campfire.OnEnd), 172 px right of the room's spawn
        [("6a", "Start")] = (172, 0),
    };
}
