namespace Celeste.Mod.SpeedrunSheet;

/// The best time of the checkpoint being practiced right now, and nothing else.
/// Moving to another checkpoint drops the previous one: one segment per export.
///
/// Fed by RunWatcher, which only calls this for a run that started at the
/// segment's first room. SpeedrunTool's own PbTimes cannot serve here — srs has
/// not set NumberOfRooms since v3.0.0, so they are cut on the player's setting
/// and do not describe sheet segments.
internal static class SessionBests {
    private const string LogTag = "srs";

    /// The game checkpoint the run started at, not the sheet row that names
    /// it. A checkpoint's row changes with the selected category, and the
    /// export screen lets the player change that from the screen itself: a
    /// time keyed on the row would vanish at the moment it is being corrected.
    ///
    /// The scope stands in for the area, and must: Celeste's AreaKey overrides
    /// Equals to return false unconditionally, and a record struct compares its
    /// fields through EqualityComparer&lt;T&gt;.Default, which calls Equals and
    /// never the type's own ==. An AreaKey in here made the key unequal to
    /// itself. ScopeOf is derived from (Area.ID, Area.Mode) anyway, so nothing
    /// is lost.
    internal readonly record struct Key(string Scope, string Anchor);

    private static Key? current;
    private static long bestTicks;

    public static void Load() {
        Everest.Events.Level.OnExit += OnExit;
        Everest.Events.Level.OnEnter += OnEnter;
    }

    public static void Unload() {
        Everest.Events.Level.OnExit -= OnExit;
        Everest.Events.Level.OnEnter -= OnEnter;
    }

    // a restart stays in the chapter and keeps practicing it; anything else
    // leaves for the overworld, where the export screen cannot be opened
    private static void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
        if (mode != LevelExit.Mode.Restart && mode != LevelExit.Mode.GoldenBerryRestart) {
            Clear($"left the level ({mode})");
        }
    }

    // loading another chapter is leaving this one, whether or not an exit was
    // seen: a savestate can cross chapters without one
    private static void OnEnter(Session session, bool fromSaveData) {
        if (session != null && current is { } held && held.Scope != SegmentAutoDetect.ScopeOf(session)) {
            Clear($"entered {SegmentAutoDetect.ScopeOf(session) ?? "an uncovered chapter"}");
        }
    }

    // deliberately NOT registered with SpeedrunTool's save states, unlike the
    // rest of srs's gameplay state: a time run is a fact about the session, not
    // about the game state, and a savestate load must not take it back
    public static void Record(SheetSegment segment, long ticks, Session session) {
        if (ticks <= 0 || !TryKeyOf(segment, session, out Key key)) {
            return;
        }

        // best of the active checkpoint, not the latest run of it: one bad
        // attempt after a good one must not throw the good one away
        if (current != key || ticks < bestTicks) {
            Logger.Log(LogLevel.Info, LogTag,
                $"session best {key.Scope}/{key.Anchor} {TimeFormat.FromTicks(ticks)}"
                + (current == key ? $" (was {TimeFormat.FromTicks(bestTicks)})" : " (first run)"));
            current = key;
            bestTicks = ticks;
        }
    }

    /// True for any row of the checkpoint the held run started at, whichever
    /// category names it: that is what lets the screen re-label a run.
    public static bool TryGet(SheetSegment segment, Session session, out long ticks) {
        ticks = 0;
        if (current is not { } held || !TryKeyOf(segment, session, out Key key) || held != key) {
            return false;
        }

        ticks = bestTicks;
        return true;
    }

    public static void Clear(string reason = null) {
        if (current is { } held) {
            Logger.Log(LogLevel.Info, LogTag,
                $"session best dropped ({reason ?? "on request"}): was {held.Scope}/{held.Anchor}");
        }

        current = null;
        bestTicks = 0;
    }

    /// Drops a run held in another chapter, checked where the run is about to
    /// be read rather than polled. Not every way of changing chapter raises an
    /// event -- a debug-console load swaps the scene directly, through neither
    /// LevelExit nor LevelEnter -- and the events cannot be relied on alone.
    public static void DropIfElsewhere(Session session) {
        if (session != null && current is { } held && held.Scope != SegmentAutoDetect.ScopeOf(session)) {
            Clear($"held in {held.Scope}, now in {SegmentAutoDetect.ScopeOf(session) ?? "an uncovered chapter"}");
        }
    }

    /// what is held, for the log: never a time the player has not run
    public static string Describe() =>
        current is { } held ? $"{held.Scope}/{held.Anchor} {TimeFormat.FromTicks(bestTicks)}" : "nothing";

    private static bool TryKeyOf(SheetSegment segment, Session session, out Key key) {
        key = default;
        string scope = session == null ? null : SegmentAutoDetect.ScopeOf(session);
        string anchor = segment == null || scope == null
            ? null
            : SegmentAutoDetect.GameNameOf(scope, segment.Name);
        if (anchor == null) {
            return false;
        }

        key = new Key(scope, anchor);
        return true;
    }
}
