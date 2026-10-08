using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// One reviewable line of the export screen.
public sealed class PendingUpdate {
    public SheetRowRef Row { get; private init; }
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
    public bool Selected { get; set; }
    /// the band the cell was read from, sent back so the script searches it alone
    public string Band { get; private init; }
    /// a key the sheet holds twice: listed, never written
    public bool Duplicate { get; private init; }
    /// which way the delta goes; null when there is nothing to compare or the
    /// times are equal at the millisecond
    public bool? Ahead { get; private init; }

    /// The time a sheet cell holds, or null when it holds none srs can read:
    /// empty, 0:00.000 or unreadable.
    public static long? TicksOf(string remoteCell) => Read(remoteCell).Ticks;

    // an hour or more is unreadable too: Speed Run Tool's format has no hours,
    // and would show the cell an hour short
    private static (bool Unreadable, long? Ticks) Read(string remoteCell) {
        long? parsed = SheetData.TryParseTime(remoteCell)?.Ticks;
        bool unreadable = (parsed == null && !string.IsNullOrWhiteSpace(remoteCell)) || parsed >= TimeSpan.TicksPerHour;
        return (unreadable, unreadable || parsed == 0 ? null : parsed);
    }

    /// remoteCell is the cell as the script read it, never a parsed time.
    ///
    /// ⚠️ Empty and unreadable must not be confused: an unreadable cell counted
    /// as empty ticks the row and overwrites it, which a Google locale writing
    /// 8,704 does on every row. 0:00.000 counts as empty, the sheet's own idiom
    /// for "no time yet".
    public static PendingUpdate Create(SheetRowRef row, string label, long localTicks, string remoteCell,
        string band = "", bool duplicate = false) {
        string localText = TimeFormat.FromTicks(localTicks);
        if (duplicate) {
            return new PendingUpdate {
                Row = row, Label = label, Band = band, Duplicate = true,
                LocalTicks = localTicks, LocalText = localText,
                RemoteText = "", RemoteCell = "", DeltaText = ExportProtocol.Localize("SRS_EXPORT_DUPLICATE"),
            };
        }

        (bool unreadable, long? remoteTicks) = Read(remoteCell);

        // compared as shown, to the millisecond, as the sheet and its script
        // compare: a finer time reads as behind the very cell it was written to
        long shown = SheetData.TryParseTime(localText)?.Ticks ?? localTicks;

        // an unreadable cell is never an improvement: we cannot tell, and the
        // safe default is to leave a time we do not understand alone
        bool improves = !unreadable && (remoteTicks == null || shown < remoteTicks.Value);

        string delta = "";
        if (unreadable) {
            delta = "?";
        } else if (remoteTicks != null) {
            delta = TimeFormat.Delta(shown - remoteTicks.Value);
        }

        return new PendingUpdate {
            Row = row,
            Label = label,
            Band = band ?? "",
            LocalTicks = localTicks,
            RemoteTicks = remoteTicks,
            LocalText = localText,
            // show the cell as it stands rather than nothing: the player is the
            // only one who can tell a locale from a typo from a note
            RemoteText = unreadable ? remoteCell.Trim()
                       : remoteTicks == null ? "" : TimeFormat.FromTicks(remoteTicks.Value),
            RemoteCell = remoteCell ?? "",
            DeltaText = delta,
            Ahead = unreadable || remoteTicks == null || shown == remoteTicks.Value ? null : shown < remoteTicks.Value,
            Selected = improves,
        };
    }
}
