using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// The best of every row run this session, fed by RunWatcher and dropped on
/// entering another chapter.
internal static class SessionBests {
    private const string LogTag = "srs";

    private static readonly RunBook book = new();

    public static void Load() {
        Everest.Events.Level.OnEnter += OnEnter;
    }

    public static void Unload() {
        Everest.Events.Level.OnEnter -= OnEnter;
    }

    // never on leaving the level: a chapter's last segment closes as its ending
    // plays, and the player exports it on coming back into the chapter
    private static void OnEnter(Session session, bool fromSaveData) => DropIfElsewhere(session);

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

    /// The row whose best improved last, looked up by name (see SheetBlock.Find).
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

    /// Drops what is held in another chapter, checked where it is read rather
    /// than polled: a debug-console load swaps the scene through neither
    /// LevelExit nor LevelEnter, so the event cannot be relied on alone.
    public static void DropIfElsewhere(Session session) {
        if (session == null) {
            return;
        }

        string held = book.Scope;
        int rows = book.All.Count;
        string scope = SegmentAutoDetect.ScopeOf(session);
        if (book.DropUnlessIn(scope)) {
            Logger.Log(LogLevel.Info, LogTag,
                $"session bests dropped: {rows} rows of {held}, now in {scope ?? "an uncovered chapter"}");
        }
    }

    /// What is held, for the log: never a time the player has not run.
    public static string Describe() =>
        book.LastImproved is { } run
            ? $"{book.All.Count} rows, last {run.Scope}/{run.Name} {TimeFormat.FromTicks(run.Ticks)}"
            : "nothing";
}
