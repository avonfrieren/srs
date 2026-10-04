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

/// Every segment that can be running, at once. RunWatcher feeds it the room
/// timer's readings and the game's events; each event returns what it closed.
/// A reading is an absolute value of SpeedrunTool's accumulator, so only
/// differences between two of them are times.
internal sealed class RunTracker(IReadOnlyList<SegmentRule> rules, IRoomMap rooms) {
    private sealed class OpenSegment(SegmentRule rule, long start) {
        public readonly SegmentRule Rule = rule;
        public readonly long Start = start;
        public Collectibles Collected;
        public readonly HashSet<string> Berries = [];
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

    /// The timer moved from 0 in this room: a standalone run starts here.
    public void TimerStarted(string scope, string room, long reading, bool control, bool launching) {
        pending.Clear();
        pendingRoom = null;
        OpenAt(scope, room, reading, control, launching, standalone: true);
    }

    /// Session.Level changed with the timer running. Closes first, then opens:
    /// the segment ending here is never the one starting here.
    public List<SegmentRecord> RoomEntered(string scope, string room, long reading, bool control, bool launching,
        EndState end) {
        List<SegmentRecord> records = CloseWhere(
            segment => segment.Rule.End == EndKind.NextStart && Rooms.EndRoomOf(segment.Rule) == room,
            reading, end);
        pending.Clear();
        pendingRoom = null;
        OpenAt(scope, room, reading, control, launching, standalone: false);
        return records;
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

    /// The summit launch ended in this room.
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

    /// TimerStopped or Completed went from false to true.
    public List<SegmentRecord> ChapterTimeStopped(long reading, EndState end) =>
        CloseWhere(
            segment => segment.Rule.End == EndKind.ChapterEnd
                       || (segment.Rule.End == EndKind.NextStart && Rooms.EndRoomOf(segment.Rule) == null),
            reading, end);

    private void OpenAt(string scope, string room, long reading, bool control, bool launching, bool standalone) {
        foreach (SegmentRule rule in rules) {
            if (rule.Scope != scope || Rooms.StartRoomOf(rule) != room) {
                continue;
            }

            if (rule.Start == StartKind.AfterLaunch) {
                // the launch's own end opens it (LaunchEnded); a timer started
                // after the landing, from a savestate there, opens it here
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

    private List<SegmentRecord> CloseWhere(System.Predicate<OpenSegment> ends, long reading, EndState end) {
        List<SegmentRecord> records = [];
        for (int i = 0; i < open.Count;) {
            OpenSegment segment = open[i];
            if (!ends(segment)) {
                i++;
                continue;
            }

            open.RemoveAt(i);
            if (Met(segment, reading, end) is { } ticks) {
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
