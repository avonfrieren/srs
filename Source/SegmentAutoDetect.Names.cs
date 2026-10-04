using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// The checkpoint name tables, split from the rest of SegmentAutoDetect (which
// needs Celeste types) so the tests can check them against the row table.
public static partial class SegmentAutoDetect {
    // (scope, game checkpoint) -> the virtual checkpoint the sheet inserts
    // right after it. The game gives 8A's finale a single "Heart of the
    // Mountain" checkpoint; the sheet times its vertical climb and its
    // horizontal chase as two segments, so "HotM Horizontal" exists only here
    // and in SheetRows, anchored by its StartRoomOverrides room. That one
    // room is read by both ends, exactly like a real checkpoint's: it is where
    // the second half starts and where the first half's run ends. Only
    // virtual checkpoints closing their chapter are supported — nothing
    // resolves the end room of one followed by a real checkpoint
    internal static readonly Dictionary<(string Scope, string GameName), string> SplitCheckpoints = new() {
        [("8a", "Heart of the Mountain")] = "HotM Horizontal",
    };

    // (scope, game checkpoint name) -> the room a run of that checkpoint's
    // segment really starts in, for the segments the sheet does not time from
    // the checkpoint's own room. Keyed by *game* name, not sheet
    // name, so both ends of a segment read the same entry: a segment ends
    // exactly where the next one starts, so an override moves the previous
    // segment's finish line with it, and the two never overlap. Rows
    // sharing an anchor share its entry
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
        // 8A's HotM Horizontal has no checkpoint of its own to start from
        // (see SplitCheckpoints): d-08 is where the climb of the d rooms tops
        // out and the chase to the right begins
        [("8a", "HotM Horizontal")] = "d-08",
    };

    // (scope, game checkpoint name) -> the head of the segment srs does not
    // time, added back to the captured time so the tier is read off the same
    // number the sheet's thresholds describe. These are the sheet's own
    // constants for the untimed start a savestate skips. 7A Start: the sheet
    // starts it at a-00 (see StartRoomOverrides) and adds a fixed 5.508s
    // afterwards for the intro room plus Madeline's landing animation. The
    // mod cannot time that part — the run is practiced from a savestate
    // placed after the landing — but it must still count it, otherwise a run
    // is compared against thresholds that include it and lands several tiers
    // too high. Prologue is timed as file time: its savestate is set after the
    // intro animation, whose 61 frames file time counts and the timer does not
    internal static readonly Dictionary<(string Scope, string GameName), TimeSpan> UntimedSegmentHead = new() {
        [("7a", "Start")] = new TimeSpan(0, 0, 0, 5, 508),
        [("Prologue", "Start")] = new TimeSpan(0, 0, 0, 1, 37),
    };

    // (scope, game checkpoint) -> the end of the segment the timer does not
    // count, added like a head. The Prologue's file time runs 33 frames past
    // the accumulator's last tick, to the LevelExit, skipped or watched
    // (measured 2026-10-03)
    internal static readonly Dictionary<(string Scope, string GameName), TimeSpan> UntimedSegmentTail = new() {
        [("Prologue", "Start")] = new TimeSpan(0, 0, 0, 0, 561),
    };

    // (scope, game checkpoint) of the segments that start once the launch
    // into their room is over. 7A's intro launches Madeline from
    // a-00-intro and she lands in a-00 in the intro-jump state, with both
    // clocks running, and the 5.508 s head already counts the landing: opening on the a-00 entry would count it twice
    internal static readonly HashSet<(string Scope, string GameName)> AfterLaunchStarts = [
        ("7a", "Start"),
    ];

    // (scope, game checkpoint) of the segments valid from their start point
    // rather than from the checkpoint before, beyond every chapter's "Start":
    // the segments after a wake-up (owner, 2026-10-03)
    internal static readonly HashSet<(string Scope, string GameName)> CurrentRoomStarts = [
        ("2a", "Awake"),
        ("5a", "Unravelling"),
        ("5b", "Through the Mirror"),
    ];

    // (scope, game checkpoint) -> the room a segment's first room must be
    // entered from for the segment to open, and for the one before it to
    // close with a time. Every Next Room segment and every segment after a
    // wake-up has one; a chapter's Start and 7A's start have none and open
    // only on a restart or at the end of the launch. Measured on 2026-10-04 by
    // replaying every chapter TAS and checking each start room's edges with
    // their entities. The rooms reached by a cutscene are entered from the
    // room the cutscene starts in: 2A's dream, 5A's and 5B's mirror, 6A's fall
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
    // world coordinates (measured 2026-10-03): the only spawn a Current Room
    // start that is not a chapter's "Start" opens from. The game's default
    // spawn will not do: in 5A's and 5B's c-00 it is the bottom one, by the
    // room's exit, while the wake-up and a chapter-select entry use the top one.
    // A Current Room start missing here never opens on a restart
    internal static readonly Dictionary<(string Scope, string GameName), (int X, int Y)> WakeUpSpawns = new() {
        [("2a", "Awake")] = (144, 1880),
        [("5a", "Unravelling")] = (-832, 1688),
        [("5b", "Through the Mirror")] = (3680, -984),
    };

    // (scope, game checkpoint) -> where the player stands, relative to a spawn
    // of the start room, on the first frame with control. Every other Current
    // Room start measured is on the spawn (2026-10-03)
    internal static readonly Dictionary<(string Scope, string GameName), (int X, int Y)> SpawnOffsets = new() {
        [("Farewell", "Start")] = (8, 0),
        // the campfire cutscene ends with the player at the bonfire, watched
        // or skipped (CS06_Campfire.OnEnd), 172 px right of the room's spawn
        [("6a", "Start")] = (172, 0),
    };
}
