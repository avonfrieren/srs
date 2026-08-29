using System;
using System.Linq;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Turns this session's segment bests into the reviewable rows of the export
/// screen. Replaces the old reading of SpeedrunTool's PbTimes: since v3.0.0 srs
/// never sets NumberOfRooms, so SpeedrunTool's own PBs are cut on the player's
/// setting and do not describe sheet segments.
internal static class ExportSource {
    /// The route the player is on, as rows: one per game checkpoint of the
    /// chapter they are in, in sheet order. The selected category picks which
    /// of a checkpoint's variants the row shows, and it can only pick one --
    /// the sheet never puts two segments of a category on one checkpoint. That
    /// invariant is what keeps the list one row per checkpoint however the
    /// category changes, so no view ever shows one checkpoint twice.
    ///
    /// Most rows carry no run: they are the sheet, shown. Only the checkpoint
    /// this session holds a time for has anything to write.
    /// The chapter the player is in, as rows, in sheet order: the checkpoints
    /// the route plays there, and nothing else. A route names its segments, so
    /// there is no variant to resolve here -- "Depths Tape" and "Depths" are
    /// two names, and a route holds one of them.
    ///
    /// A null route, or the "All" entry, filters nothing: every row of the
    /// chapter shows, variants side by side.
    ///
    /// Most rows carry no run: they are the sheet, shown. Only the checkpoint
    /// this session holds a time for has anything to write.
    public static List<PendingUpdate> Collect(Session session, SheetRoute route) {
        List<PendingUpdate> updates = [];
        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        string scope = SegmentAutoDetect.ScopeOf(session);
        if (block == null || scope == null) {
            return updates;
        }


        // the one place a held run is read, so the one place worth checking it
        // still belongs to the chapter the player is in
        SessionBests.DropIfElsewhere(session);

        string chapter = SegmentAutoDetect.ChapterOf(session);
        IReadOnlyList<string> played = route is { FiltersRows: true } ? route.Checkpoints(scope) : null;

        // parallel to updates: an "All" view lists a checkpoint's variants side
        // by side, and the held run answers to all of them
        List<string> anchors = [];

        foreach (SheetSegment segment in InOrder(block, chapter, played)) {
            if (!SheetLabels.TryMap(segment.Chapter, segment.Name, out SheetRowRef row)) {
                continue;
            }

            // no anchor in this scope means the other side of a folded chapter,
            // which is made of runs this player cannot have just done
            string anchor = SegmentAutoDetect.GameNameOf(scope, segment.Name);
            if (anchor == null) {
                continue;
            }

            long ticks = SessionBests.TryGet(segment, session, out long held) ? held : 0L;
            updates.Add(Build(row, segment, ticks, session));
            anchors.Add(anchor);
        }

        PendingUpdate.UntickSharedCheckpoints(updates, anchors);
        return updates;
    }

    /// A route's rows in the order it plays them, which is the order its
    /// category tab lists them and not the order the standards tab does: a
    /// 2a-heart route visits the chapter for the heart before playing it, and
    /// the standards tab has the plain row first. Falls back to sheet order
    /// where there is no route to ask, which is what "All" wants.
    private static IEnumerable<SheetSegment> InOrder(SheetBlock block, string chapter,
        IReadOnlyList<string> played) {
        if (played == null) {
            return block.Segments.Where(segment => segment.Chapter == chapter);
        }

        Dictionary<string, SheetSegment> byName = [];
        foreach (SheetSegment segment in block.Segments) {
            if (segment.Chapter == chapter) {
                byName[segment.Name] = segment;
            }
        }

        List<SheetSegment> ordered = [];
        foreach (string name in played) {
            if (byName.TryGetValue(name, out SheetSegment segment)) {
                ordered.Add(segment);
            }
        }

        return ordered;
    }

    /// srs folds 6A and 6B into one chapter "6a/b" and re-prefixes only the
    /// names both sides share ("6a Rock Bottom"). On screen that prefix is
    /// noise, the player knows which side they are on, so it comes off. Nothing
    /// collides once it does: CandidatesFor anchors on the game checkpoint of
    /// the current scope, and the other side has none in it.
    public static string DisplayName(SheetSegment segment, Session session) {
        string side = SegmentAutoDetect.ScopeOf(session);
        return side != null && segment.Name.StartsWith(side + " ", StringComparison.Ordinal)
            ? segment.Name[(side.Length + 1)..]
            : segment.Name;
    }

    /// Rebuilds a row against a segment, remote time included: the sheet value,
    /// the delta and whether it improves all change with the target.
    public static PendingUpdate Build(SheetRowRef row, SheetSegment segment, long ticks, Session session) {
        // the raw cell, not a parsed time: PendingUpdate has to tell an empty
        // cell from one it cannot read, and only the cell itself says which
        string remote = RemoteBests.TryGet(row, out RemoteRow remoteRow) ? remoteRow.Time : null;

        return PendingUpdate.Create(row, DisplayName(segment, session), ticks, remote, segment);
    }
}
