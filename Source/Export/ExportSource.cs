using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Turns this session's segment best into the reviewable row of the export
/// screen. Not SpeedrunTool's PbTimes: srs has not set NumberOfRooms since
/// v3.0.0, so those are cut on the player's setting and describe no sheet segment.
internal static class ExportSource {
    /// The one row there is to export: the segment the mod has selected, when
    /// this session holds a run of it.
    ///
    /// ⚠️ The chapter check cannot be left to SessionBests. Its key is (scope,
    /// game checkpoint) and "Start" resolves to the same anchor in every chapter
    /// that has one, so without it one run exports into six chapters at once.
    public static List<PendingUpdate> Collect(Session session) {
        List<PendingUpdate> updates = [];

        // the one place a held run is read, so the one place worth checking it
        // still belongs to the chapter the player is in
        SessionBests.DropIfElsewhere(session);

        SheetSegment segment = SegmentSelector.Current;
        if (segment == null
            || segment.Chapter != SegmentAutoDetect.ChapterOf(session)
            || !SheetLabels.TryMap(segment.Chapter, segment.Name, out SheetRowRef row)
            || !SessionBests.TryGet(segment, session, out long ticks)) {
            return updates;
        }

        updates.Add(Build(row, segment, ticks, session));
        return updates;
    }

    /// The mappable segments anchored on the same game checkpoint, in sheet
    /// order: auto-detect cannot tell those apart, and this is what the arrows
    /// retarget onto ({Hollows, Hollows Tape} and its like).
    ///
    /// ⚠️ Never the whole chapter. On 7a that is seven different checkpoints,
    /// and two arrow presses write 0m's time into 3000m's row.
    public static List<SheetSegment> CandidatesFor(SheetSegment segment, Session session) {
        List<SheetSegment> candidates = [];
        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        if (block == null || segment == null) {
            return candidates;
        }

        string scope = SegmentAutoDetect.ScopeOf(session);
        string anchor = SegmentAutoDetect.GameNameOf(scope, segment.Name);
        if (anchor == null) {
            // no game checkpoint behind this row: nothing can be said about
            // what shares its start room, so offer nothing rather than a chapter
            return candidates;
        }

        foreach (SheetSegment other in block.Segments) {
            if (other.Chapter != segment.Chapter
                || !SheetLabels.TryMap(other.Chapter, other.Name, out _)
                || SegmentAutoDetect.GameNameOf(scope, other.Name) != anchor) {
                continue;
            }

            candidates.Add(other);
        }

        return candidates;
    }

    /// srs folds 6A and 6B into "6a/b" and re-prefixes the names both sides
    /// share ("6a Rock Bottom"). On screen that prefix is noise, and dropping it
    /// collides with nothing: CandidatesFor anchors on the current scope.
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
