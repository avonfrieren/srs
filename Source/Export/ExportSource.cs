using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Turns this session's segment bests into the reviewable rows of the export
/// screen. Replaces the old reading of SpeedrunTool's PbTimes: since v3.0.0 srs
/// never sets NumberOfRooms, so SpeedrunTool's own PBs are cut on the player's
/// setting and do not describe sheet segments.
internal static class ExportSource {
    /// The one row there is to export: the segment the mod has selected, when
    /// this session holds a run of it. The screen is an export screen and not a
    /// way of looking at the sheet -- a row carrying no run has nothing to write
    /// and nothing to decide.
    ///
    /// ⚠️ The chapter is checked here and cannot be left to SessionBests. Its
    /// key is (scope, game checkpoint), and GameNameOf takes a name without a
    /// chapter, so "Start" resolves to the same anchor in every chapter that has
    /// one. Walking the whole sheet and keeping what the held run answers to
    /// therefore returns the Start row of 1a, 2a, 3a, 4a, 8a and Farewell at
    /// once, most of them ticked, and confirming writes one run into six
    /// chapters. Seen on screen on 2026-08-30.
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
    /// order. Auto-detect cannot tell those apart, since they start in the same
    /// room, and this is what the export screen offers to retarget onto: the
    /// pairs the feature was written for, {Hollows, Hollows Tape} and its like.
    /// The whole chapter would be wrong here, not merely wide: on 7a it offers
    /// seven rows that begin at seven different checkpoints, and two arrow
    /// presses write the time of 0m into the row of 3000m, which the script
    /// accepts because it only checks that the row exists.
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
