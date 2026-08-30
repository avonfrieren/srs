using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// One reviewable line of the export screen.
public sealed class PendingUpdate {
    public SheetRowRef Row { get; private init; }
    /// the sheet segment this time is being written to; null in tests
    public SheetSegment Segment { get; private init; }
    public string Label { get; private init; }
    public long LocalTicks { get; private init; }
    public long? RemoteTicks { get; private init; }
    public string LocalText { get; private init; }
    public string RemoteText { get; private init; }
    /// the cell exactly as the script read it, kept alongside the parsed value:
    /// it is what the write compares against, and the sheet displays "1:36.9"
    /// where RemoteText reformats to "1:36.900"
    public string RemoteCell { get; private init; }
    public string DeltaText { get; private init; }
    public bool WillImprove { get; private init; }
    /// the sheet holds something in that cell and this mod cannot read it
    public bool RemoteUnreadable { get; private init; }
    public bool Selected { get; set; }

    /// remoteCell is the cell as the script read it, never a parsed time.
    ///
    /// ⚠️ Empty and unreadable must not be confused: an unreadable cell counted
    /// as empty ticks the row and overwrites it, which a Google locale writing
    /// 8,704 does on every row. 0:00.000 counts as empty, the sheet's own idiom
    /// for "no time yet" (as in TierComparison's threshold > TimeSpan.Zero).
    public static PendingUpdate Create(SheetRowRef row, string label, long localTicks, string remoteCell,
        SheetSegment segment = null) {
        long? parsedTicks = SheetData.TryParseTime(remoteCell)?.Ticks;
        bool unreadable = parsedTicks == null && !string.IsNullOrWhiteSpace(remoteCell);
        long? remoteTicks = parsedTicks == 0 ? null : parsedTicks;

        // an unreadable cell is never an improvement: we cannot tell, and the
        // safe default is to leave a time we do not understand alone
        bool improves = !unreadable && (remoteTicks == null || localTicks < remoteTicks.Value);

        string delta = "";
        if (unreadable) {
            delta = "?";
        } else if (remoteTicks != null) {
            delta = TimeFormat.Delta(localTicks - remoteTicks.Value);
        }

        return new PendingUpdate {
            Row = row,
            Segment = segment,
            Label = label,
            LocalTicks = localTicks,
            RemoteTicks = remoteTicks,
            LocalText = TimeFormat.FromTicks(localTicks),
            // show the cell as it stands rather than nothing: the player is the
            // only one who can tell a locale from a typo from a note
            RemoteText = unreadable ? remoteCell.Trim()
                       : remoteTicks == null ? "" : TimeFormat.FromTicks(remoteTicks.Value),
            RemoteCell = remoteCell ?? "",
            DeltaText = delta,
            WillImprove = improves,
            RemoteUnreadable = unreadable,
            Selected = improves,
        };
    }
}
