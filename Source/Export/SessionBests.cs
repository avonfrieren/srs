using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// The best of every row run this session, fed by RunWatcher and dropped on
/// leaving the chapter.
internal static class SessionBests {
    private const string LogTag = "srs";

    private static readonly RunBook book = new();

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
        if (session != null && book.Scope is { } scope && scope != SegmentAutoDetect.ScopeOf(session)) {
            Clear($"entered {SegmentAutoDetect.ScopeOf(session) ?? "an uncovered chapter"}");
        }
    }

    // deliberately NOT registered with SpeedrunTool's save states: a time run
    // is a fact about the session, and a load must not take it back
    public static void Record(IReadOnlyList<SegmentRecord> records, Session session) {
        string scope = session == null ? null : SegmentAutoDetect.ScopeOf(session);
        List<SegmentRecord> here = [];
        foreach (SegmentRecord record in records) {
            if (record.Rule.Scope == scope) {
                here.Add(record);
            }
        }

        foreach (RunBook.Run run in book.Offer(here)) {
            Logger.Log(LogLevel.Info, LogTag, $"session best {run.Scope}/{run.Name} {TimeFormat.FromTicks(run.Ticks)}");
        }
    }

    /// The row whose best improved last, looked up by name in the sheet as it
    /// is now: SheetImporter.Data is reassigned from a worker.
    public static bool TryGet(out SheetSegment segment, out long ticks) {
        segment = null;
        ticks = 0;
        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        if (book.LastImproved is not { } run || block == null) {
            return false;
        }

        segment = block.Find(run.Chapter, run.Name);
        ticks = run.Ticks;
        return segment != null;
    }

    public static void Clear(string reason = null) {
        if (book.Scope is { } scope) {
            Logger.Log(LogLevel.Info, LogTag,
                $"session bests dropped ({reason ?? "on request"}): {book.All.Count} rows of {scope}");
        }

        book.Clear();
    }

    /// Drops what is held in another chapter, checked where it is read rather
    /// than polled: a debug-console load swaps the scene through neither
    /// LevelExit nor LevelEnter, so the events cannot be relied on alone.
    public static void DropIfElsewhere(Session session) {
        if (session != null && book.Scope is { } scope && scope != SegmentAutoDetect.ScopeOf(session)) {
            Clear($"held in {scope}, now in {SegmentAutoDetect.ScopeOf(session) ?? "an uncovered chapter"}");
        }
    }

    /// What is held, for the log: never a time the player has not run.
    public static string Describe() =>
        book.LastImproved is { } run
            ? $"{book.All.Count} rows, last {run.Scope}/{run.Name} {TimeFormat.FromTicks(run.Ticks)}"
            : "nothing";
}
