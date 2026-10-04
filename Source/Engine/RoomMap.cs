using System.Collections.Generic;
using Celeste.Mod;

namespace Celeste.Mod.SpeedrunSheet;

/// The rooms of the area being played, for the rules of its scope. Built from
/// AreaData and the English dialog, so on the game thread only, and cached per
/// area: neither changes while it is played.
internal sealed class RoomMap : IRoomMap {
    private static RoomMap current;

    private readonly AreaKey area;
    private readonly string scope;
    private readonly string firstRoom;
    private readonly CheckpointData[] checkpoints;
    private readonly HashSet<string> warned = [];

    private RoomMap(Session session) {
        area = session.Area;
        scope = SegmentAutoDetect.ScopeOf(session);
        firstRoom = session.MapData?.StartLevel()?.Name;
        checkpoints = AreaData.Get(session.Area)?.Mode[(int)session.Area.Mode]?.Checkpoints ?? [];
    }

    public static RoomMap For(Session session) {
        if (current == null || !current.area.Equals(session.Area)) {
            current = new RoomMap(session);
        }

        return current;
    }

    public string StartRoomOf(SegmentRule rule) =>
        rule.Scope == scope ? StartRoomOf(rule.Anchor) : null;

    // where a segment ends: exactly where the next one starts, so two segments
    // never overlap. Null = no next checkpoint, the chapter's end ends it
    public string EndRoomOf(SegmentRule rule) {
        if (rule.Scope != scope || checkpoints.Length == 0) {
            return null;
        }

        // a checkpoint the sheet cuts in two ends where its second half starts
        if (SegmentAutoDetect.SplitCheckpoints.TryGetValue((scope, rule.Anchor), out string secondHalf)) {
            return StartRoomOf(secondHalf);
        }

        // CheckpointData only lists the non-start checkpoints
        if (rule.Anchor == "Start") {
            return StartRoomOf(EnglishName(checkpoints[0]));
        }

        for (int i = 0; i < checkpoints.Length - 1; i++) {
            if (EnglishName(checkpoints[i]) == rule.Anchor) {
                string next = StartRoomOf(EnglishName(checkpoints[i + 1]));
                if (next == null && warned.Add(rule.Anchor)) {
                    Logger.Log(LogLevel.Warn, "srs", $"room map: no start room for the checkpoint after {rule.Anchor} in {scope}");
                }

                return next;
            }
        }

        return null;
    }

    // no imported row requires berries yet (SegmentRulesTests pins it): the
    // map's berries per checkpoint come with the rows that need them
    public IReadOnlyCollection<string> BerriesOf(SegmentRule rule) => [];

    // the override when the sheet does not start the segment at the
    // checkpoint's own room (2A Awake, 7A Start, 8A HotM Horizontal), the map's
    // first room for "Start", the checkpoint's room otherwise
    private string StartRoomOf(string gameName) {
        if (SegmentAutoDetect.StartRoomOverrides.TryGetValue((scope, gameName), out string overridden)) {
            return overridden;
        }

        if (gameName == "Start") {
            return firstRoom;
        }

        foreach (CheckpointData checkpoint in checkpoints) {
            if (EnglishName(checkpoint) == gameName) {
                return checkpoint.Level;
            }
        }

        return null;
    }

    // CheckpointData.Name is a dialog key, translated: the tables must not
    // depend on the player's language
    private static string EnglishName(CheckpointData checkpoint) =>
        Dialog.Clean(checkpoint.Name, Dialog.Languages["english"]);
}
