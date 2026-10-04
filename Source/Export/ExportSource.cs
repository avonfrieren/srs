using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Turns this session's segment best into the reviewable row of the export
/// screen. Not SpeedrunTool's PbTimes: srs never sets NumberOfRooms, so those
/// are cut on the player's setting and describe no sheet segment.
internal static class ExportSource {
    /// The one row there is to export: the row whose session best improved
    /// last. Labelled by the row the run closed, never by its checkpoint: a
    /// Hollows Tape run must not label its time Hollows.
    public static List<PendingUpdate> Collect(Session session) {
        List<PendingUpdate> updates = [];

        // the one place the session's bests are read, so the one place worth
        // checking they still belong to the chapter the player is in
        SessionBests.DropIfElsewhere(session);

        if (!SessionBests.TryGet(out SheetSegment segment, out long ticks)
            || segment.Chapter != SegmentAutoDetect.ChapterOf(session)
            || !SheetLabels.TryMap(segment.Chapter, segment.Name, out SheetRowRef row)) {
            return updates;
        }

        updates.Add(Build(row, segment, ticks, session));
        return updates;
    }

    /// srs folds 6A and 6B into "6a/b" and re-prefixes the names both sides
    /// share ("6a Rock Bottom"). On screen that prefix is noise, and dropping it
    /// collides with nothing within a single scope.
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
