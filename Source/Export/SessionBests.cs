namespace Celeste.Mod.SpeedrunSheet;

/// The best time of the checkpoint being practised right now, and nothing else.
/// Moving to another checkpoint drops the previous one: one segment per export.
///
/// Fed by RunWatcher, which only calls this for a run that started at the
/// segment's first room. SpeedrunTool's own PbTimes cannot serve here — srs has
/// not set NumberOfRooms since v3.0.0, so they are cut on the player's setting
/// and do not describe sheet segments.
internal static class SessionBests {
    internal readonly record struct Key(string Chapter, string Name, SegmentCategory Category);

    private static Key? current;
    private static long bestTicks;

    // deliberately NOT registered with SpeedrunTool's save states, unlike the
    // rest of srs's gameplay state: a time run is a fact about the session, not
    // about the game state, and a savestate load must not take it back
    public static void Record(SheetSegment segment, long ticks) {
        if (segment == null || ticks <= 0) {
            return;
        }

        Key key = KeyOf(segment);
        // best of the active checkpoint, not the latest run of it: one bad
        // attempt after a good one must not throw the good one away
        if (current != key || ticks < bestTicks) {
            current = key;
            bestTicks = ticks;
        }
    }

    public static bool TryGet(SheetSegment segment, out long ticks) {
        ticks = 0;
        if (current is not { } key || segment == null || key != KeyOf(segment)) {
            return false;
        }

        ticks = bestTicks;
        return true;
    }

    private static Key KeyOf(SheetSegment segment) => new(segment.Chapter, segment.Name, segment.Category);
}
