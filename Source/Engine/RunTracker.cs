using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Rooms for rules, which only the game thread can resolve (AreaData, the
/// English dialog). RoomMap is the game's; the tests use a table.
internal interface IRoomMap {
    /// The room a run of the rule starts in; null when it cannot be resolved.
    string StartRoomOf(SegmentRule rule);

    /// The room a NextStart rule ends in; null when it has none and ends when
    /// chapter time stops.
    string EndRoomOf(SegmentRule rule);

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
    private sealed class OpenSegment(SegmentRule rule, long start) {
        public readonly SegmentRule Rule = rule;
        public readonly long Start = start;
        public Collectibles Collected;
        public readonly HashSet<string> Berries = [];
        public bool Disqualified;
    }

    private readonly List<OpenSegment> open = [];
    // starts reached without control, waiting for it to come back in pendingRoom
    private readonly List<SegmentRule> pending = [];
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

    /// The start room of the last segment start room entered: the checkpoint
    /// the player is in. RunWatcher saves it with each savestate and puts it
    /// back on a load; null when unknown.
    public string Checkpoint { get; set; }

    /// A new attempt (a savestate load, a level from the loader, a room
    /// teleport, a first-room reset): nothing open is recorded.
    /// The Current Room segments starting in this room then open where atStart
    /// says the player is at their start; nothing else does.
    public void Restart(string scope, string room, long reading, bool control, bool launching,
        Func<SegmentRule, bool> atStart) {
        Drop();
        if (IsStartRoom(scope, room)) {
            Checkpoint = room;
        }

        OpenAt(scope, room, reading, control, launching, standalone: true,
            rule => rule.Setup == StartSetup.CurrentRoom && atStart(rule));
    }

    /// Session.Level changed. Closes first, then opens: the segment ending here
    /// is never the one starting here. A start opens only when entered from the
    /// checkpoint before it, or when this entry closed a segment ending here,
    /// so a backtrack into a start room, or a return to the chapter's first
    /// room, opens nothing.
    public List<SegmentRecord> RoomEntered(string scope, string room, long reading, bool control, bool launching,
        EndState end) {
        List<SegmentRecord> records = CloseWhere(
            segment => segment.Rule.End == EndKind.NextStart && Rooms.EndRoomOf(segment.Rule) == room,
            reading, end, out int closed);
        pending.Clear();
        pendingRoom = null;
        if (IsStartRoom(scope, room)) {
            string from = Checkpoint;
            Checkpoint = room;
            // closing a segment that ends here (met, unmet or disqualified) also
            // opens: 6A's watched fall passes through Hollows' room 04 before
            // Lake's 00, which moves the checkpoint off 6a Start's
            if (closed > 0 || Follows(scope, from, room)) {
                OpenAt(scope, room, reading, control, launching, standalone: false, _ => true);
            }
        }

        return records;
    }

    private bool IsStartRoom(string scope, string room) {
        foreach (SegmentRule rule in rules) {
            if (rule.Scope == scope && Rooms.StartRoomOf(rule) == room) {
                return true;
            }
        }

        return false;
    }

    // a segment of the checkpoint the player was in ends in this room
    private bool Follows(string scope, string from, string room) {
        if (from == null) {
            return false;
        }

        foreach (SegmentRule rule in rules) {
            if (rule.Scope == scope && Rooms.StartRoomOf(rule) == from && Rooms.EndRoomOf(rule) == room) {
                return true;
            }
        }

        return false;
    }

    /// Control came back in this room: a start reached without it opens now.
    public void ControlReturned(string room, long reading) {
        if (pendingRoom == room) {
            foreach (SegmentRule rule in pending) {
                OpenIfClosed(rule, reading);
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
            reading, end, out _);
    }

    public void BerryCollected(string berry) {
        foreach (OpenSegment segment in open) {
            segment.Berries.Add(berry);
        }
    }

    /// TimerStopped or Completed went from false to true.
    public List<SegmentRecord> ChapterTimeStopped(long reading, EndState end) =>
        CloseWhere(
            segment => segment.Rule.End == EndKind.ChapterEnd
                       || (segment.Rule.End == EndKind.NextStart && Rooms.EndRoomOf(segment.Rule) == null),
            reading, end, out _);

    private void OpenAt(string scope, string room, long reading, bool control, bool launching, bool standalone,
        Func<SegmentRule, bool> opens) {
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
                pending.Add(rule);
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

    private List<SegmentRecord> CloseWhere(System.Predicate<OpenSegment> ends, long reading, EndState end,
        out int removed) {
        removed = 0;
        List<SegmentRecord> records = [];
        for (int i = 0; i < open.Count;) {
            OpenSegment segment = open[i];
            if (!ends(segment)) {
                i++;
                continue;
            }

            open.RemoveAt(i);
            removed++;
            if (!segment.Disqualified && Met(segment, reading, end) is { } ticks) {
                records.Add(new SegmentRecord(segment.Rule, ticks));
            }
        }

        records.Sort((a, b) => a.Rule.Order.CompareTo(b.Rule.Order));
        return records;
    }

    // the time, or null when the run did not meet the row's requirements
    private long? Met(OpenSegment segment, long reading, EndState end) {
        SegmentRule rule = segment.Rule;
        long elapsed = reading - segment.Start;
        if (elapsed <= 0
            || (segment.Collected & rule.Requires) != rule.Requires
            || (rule.Dashes is { } dashes && end.Dashes != dashes)
            || (rule.RequiresBerries && !HasEveryBerry(segment, end))) {
            return null;
        }

        return elapsed + rule.HeadTicks + rule.TailTicks;
    }

    // a berry still following at the end counts (owner, 2026-10-03)
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
