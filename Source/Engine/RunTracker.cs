using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Rooms for rules, which only the game thread can resolve (AreaData, the
/// English dialog). RoomMap is the game's; the tests use a table.
internal interface IRoomMap {
    /// The room a run of the rule starts in; null when it cannot be resolved.
    string StartRoomOf(SegmentRule rule);

    /// The room a NextStart rule ends in; null when it has none and ends when
    /// chapter time stops. For a Restart rule, the room its segment ends in.
    string EndRoomOf(SegmentRule rule);

    /// The room the rule's start room must be entered from; null when the rule
    /// never opens on an entry (a chapter's Start, 7A's start).
    string EntryRoomOf(SegmentRule rule);

    /// The red berries of the rule's anchor checkpoint, as EntityID keys.
    IReadOnlyCollection<string> BerriesOf(SegmentRule rule);
}

/// What the run carries when a segment ends, for the requirement checks.
internal readonly record struct EndState(int Dashes, IReadOnlyCollection<string> FollowingBerries) {
    public static EndState With(int dashes) => new(dashes, []);
}

/// A closed segment whose requirements the run met, with its time.
internal readonly record struct SegmentRecord(SegmentRule Rule, long Ticks);

/// Every segment that can be running, at once. RunWatcher feeds it chapter-time
/// readings and the game's events; each event returns what it closed.
/// A reading is chapter time (`Session.Time` ticks), so only differences
/// between two of them are times.
internal sealed class RunTracker(IReadOnlyList<SegmentRule> rules, IRoomMap rooms) {
    // Speed Run Tool's format prints minutes and seconds, never hours: a time
    // this long would be shown and exported an hour short
    private const long Longest = TimeSpan.TicksPerHour;

    private sealed class OpenSegment(SegmentRule rule, long start) {
        public readonly SegmentRule Rule = rule;
        public readonly long Start = start;
        public Collectibles Collected;
        public readonly HashSet<string> Berries = [];
        public bool Disqualified;
    }

    private readonly List<OpenSegment> open = [];
    // starts reached without control, waiting for it to come back in
    // pendingRoom. FromLoad: a savestate loaded without control, whose start
    // is tested where the player appears
    private readonly List<(SegmentRule Rule, bool FromLoad)> pending = [];
    private string pendingRoom;

    /// Swapped by RunWatcher when the area changes.
    public IRoomMap Rooms { get; set; } = rooms;

    public IEnumerable<SegmentRule> Open {
        get {
            foreach (OpenSegment segment in open) {
                yield return segment.Rule;
            }
        }
    }

    /// The timeline changed under the open segments: nothing open is recorded.
    public void Drop() {
        open.Clear();
        pending.Clear();
        pendingRoom = null;
    }

    /// Everything open stays open but can no longer be recorded, and starts
    /// waiting for control are forgotten. For a stretch the run did not play,
    /// like 6A's watched fall: it must not be timed, yet the segment after it
    /// must still open, which needs the disqualified one to close on its end.
    public void Disqualify() {
        foreach (OpenSegment segment in open) {
            segment.Disqualified = true;
        }

        pending.Clear();
        pendingRoom = null;
    }

    /// A new attempt (a level from the loader, a room teleport, a first-room
    /// reset, a savestate loaded with control): nothing open is recorded.
    /// The Current Room segments starting in this room then open where atStart
    /// says the player is at their start; nothing else does.
    public void Restart(string scope, string room, long reading, bool control, bool launching,
        Func<SegmentRule, bool> atStart) {
        Drop();
        OpenAt(scope, room, reading, control, launching, standalone: true,
            rule => rule.Setup == StartSetup.CurrentRoom && atStart(rule), fromLoad: false);
    }

    /// A savestate loaded without control (mid-wake-up, mid-respawn, mid-intro):
    /// nothing open is recorded, and the Current Room segments starting in this
    /// room wait for the player to appear, where ControlReturned tests them.
    public void RestartAtAppearance(string scope, string room) {
        Drop();
        OpenAt(scope, room, 0, control: false, launching: false, standalone: true,
            rule => rule.Setup == StartSetup.CurrentRoom, fromLoad: true);
    }

    /// The player walked into this room from another. Closes first, then opens:
    /// the segment ending here is never the one starting here. Only an entry
    /// from the entry room of a segment starting here counts: it closes the
    /// segment ending here with a time and opens the one starting here. Any
    /// other way in may be a shortcut, and drops the segment ending here
    /// unrecorded. A Restart row whose segment ends here is dropped on any way
    /// in: a restart after it is not a run of the row.
    public List<SegmentRecord> RoomEntered(string scope, string from, string room, long reading, bool control,
        bool launching, EndState end) {
        bool entry = IsEntry(scope, from, room);
        List<SegmentRecord> records = CloseWhere(
            segment => segment.Rule.End == EndKind.NextStart && Rooms.EndRoomOf(segment.Rule) == room,
            reading, end, record: entry);
        CloseWhere(segment => segment.Rule.End == EndKind.Restart && Rooms.EndRoomOf(segment.Rule) == room,
            reading, end, record: false);
        pending.Clear();
        pendingRoom = null;
        if (entry) {
            OpenAt(scope, room, reading, control, launching, standalone: false,
                rule => Rooms.EntryRoomOf(rule) == from, fromLoad: false);
        }

        return records;
    }

    // a segment starting in this room is entered from that one
    private bool IsEntry(string scope, string from, string room) {
        if (from == null) {
            return false;
        }

        foreach (SegmentRule rule in rules) {
            if (rule.Scope == scope && Rooms.StartRoomOf(rule) == room && Rooms.EntryRoomOf(rule) == from) {
                return true;
            }
        }

        return false;
    }

    /// Control came back in this room: a start reached without it opens now.
    /// One a savestate load left waiting opens only where atAppearance says
    /// the player appeared at its start.
    public void ControlReturned(string room, long reading, Func<SegmentRule, bool> atAppearance) {
        if (pendingRoom == room) {
            foreach ((SegmentRule rule, bool fromLoad) in pending) {
                if (!fromLoad || atAppearance(rule)) {
                    OpenIfClosed(rule, reading);
                }
            }
        }

        pending.Clear();
        pendingRoom = null;
    }

    /// The launch ended in this room.
    public void LaunchEnded(string scope, string room, long reading) {
        foreach (SegmentRule rule in rules) {
            if (rule.Scope == scope && rule.Start == StartKind.AfterLaunch && Rooms.StartRoomOf(rule) == room) {
                OpenIfClosed(rule, reading);
            }
        }
    }

    /// A collect, between updates: it counts for every segment already open,
    /// and closes those that end on it.
    public List<SegmentRecord> Collected(Collectibles kind, long reading, EndState end) {
        foreach (OpenSegment segment in open) {
            segment.Collected |= kind;
        }

        return CloseWhere(
            segment => segment.Rule.End == EndKind.Collect
                       && (segment.Collected & segment.Rule.EndsOn) == segment.Rule.EndsOn,
            reading, end);
    }

    public void BerryCollected(string berry) {
        foreach (OpenSegment segment in open) {
            segment.Berries.Add(berry);
        }
    }

    /// TimerStopped or Completed went from false to true. `reading` is the one before
    /// the frame; `counted` includes it, as the game's chapter time does, for a whole chapter.
    public List<SegmentRecord> ChapterTimeStopped(long reading, long counted, EndState end) {
        List<SegmentRecord> records = CloseWhere(segment => segment.Rule.End == EndKind.ChapterEnd, counted, end);
        records.AddRange(CloseWhere(
            segment => segment.Rule.End == EndKind.NextStart && Rooms.EndRoomOf(segment.Rule) == null,
            reading, end));
        records.Sort((a, b) => a.Rule.Order.CompareTo(b.Rule.Order));
        return records;
    }

    /// Restart Chapter left the level, with the old session's last reading: the
    /// segments ending on it close, and everything else open is dropped
    /// unrecorded. The new level's Restart then opens what starts there.
    public List<SegmentRecord> ChapterRestarted(long reading, EndState end) {
        List<SegmentRecord> records = CloseWhere(segment => segment.Rule.End == EndKind.Restart, reading, end);
        Drop();
        return records;
    }

    private void OpenAt(string scope, string room, long reading, bool control, bool launching, bool standalone,
        Func<SegmentRule, bool> opens, bool fromLoad) {
        foreach (SegmentRule rule in rules) {
            if (rule.Scope != scope || Rooms.StartRoomOf(rule) != room || !opens(rule)) {
                continue;
            }

            if (rule.Start == StartKind.AfterLaunch) {
                // the launch's own end opens it (LaunchEnded); a load after the
                // landing opens it here
                if (standalone && control && !launching) {
                    OpenIfClosed(rule, reading);
                }

                continue;
            }

            if (control) {
                OpenIfClosed(rule, reading);
            } else {
                pending.Add((rule, fromLoad));
                pendingRoom = room;
            }
        }
    }

    // a segment already open stays open: a route looping through its start
    // room would lose the loop
    private void OpenIfClosed(SegmentRule rule, long reading) {
        foreach (OpenSegment segment in open) {
            if (segment.Rule == rule) {
                return;
            }
        }

        open.Add(new OpenSegment(rule, reading));
    }

    // record false: the segments that end now are dropped, not recorded
    private List<SegmentRecord> CloseWhere(Predicate<OpenSegment> ends, long reading, EndState end,
        bool record = true) {
        List<SegmentRecord> records = [];
        for (int i = 0; i < open.Count;) {
            OpenSegment segment = open[i];
            if (!ends(segment)) {
                i++;
                continue;
            }

            open.RemoveAt(i);
            if (record && !segment.Disqualified && Met(segment, reading, end) is { } ticks) {
                records.Add(new SegmentRecord(segment.Rule, ticks));
            }
        }

        records.Sort((a, b) => a.Rule.Order.CompareTo(b.Rule.Order));
        return records;
    }

    // the time, or null when the run did not meet the row's requirements or
    // took an hour or more
    private long? Met(OpenSegment segment, long reading, EndState end) {
        SegmentRule rule = segment.Rule;
        long elapsed = reading - segment.Start;
        if (elapsed <= 0
            || (segment.Collected & rule.Requires) != rule.Requires
            || (rule.Dashes is { } dashes && end.Dashes != dashes)
            || (rule.RequiresBerries && !HasEveryBerry(segment, end))) {
            return null;
        }

        long ticks = elapsed + rule.HeadTicks + rule.TailTicks;
        return ticks < Longest ? ticks : null;
    }

    // a berry still following at the end counts (owner decision)
    private bool HasEveryBerry(OpenSegment segment, EndState end) {
        foreach (string berry in Rooms.BerriesOf(segment.Rule)) {
            if (!segment.Berries.Contains(berry) && !Contains(end.FollowingBerries, berry)) {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(IReadOnlyCollection<string> berries, string berry) {
        foreach (string candidate in berries) {
            if (candidate == berry) {
                return true;
            }
        }

        return false;
    }
}
