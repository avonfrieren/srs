using System;
using System.Collections.Generic;
using System.Globalization;

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
    public string DeltaText { get; private init; }
    public bool WillImprove { get; private init; }
    /// this session holds no run of the segment: the row shows the sheet and
    /// nothing else, and there is nothing to write
    public bool HasLocal { get; private init; }
    /// the sheet holds something in that cell and this mod cannot read it
    public bool RemoteUnreadable { get; private init; }
    public bool Selected { get; set; }

    /// A run belongs to one row. It is held against the game checkpoint it
    /// started at, so every sheet row of that checkpoint can claim it -- which
    /// is what lets the player re-label a run by changing the view, and what
    /// puts a plain row and its cassette or heart variant side by side in an
    /// "All" view, both showing the same time.
    ///
    /// Nothing may be ticked by default in that case. A variant almost always
    /// has the longer reference time, and usually none at all, so "it improves
    /// on the sheet" is trivially true for a row the player never ran: a
    /// confirmed export would write a fabricated time into it. Where the view
    /// lists more than one row of a checkpoint, the player says which, and the
    /// screen asks rather than guesses.
    ///
    /// anchors is parallel to updates: the game checkpoint each row sits on.
    public static void UntickSharedCheckpoints(List<PendingUpdate> updates, IReadOnlyList<string> anchors) {
        if (updates == null || anchors == null || updates.Count != anchors.Count) {
            return;
        }

        Dictionary<string, int> claiming = [];
        for (int i = 0; i < updates.Count; i++) {
            if (updates[i].HasLocal && anchors[i] != null) {
                claiming[anchors[i]] = claiming.TryGetValue(anchors[i], out int seen) ? seen + 1 : 1;
            }
        }

        for (int i = 0; i < updates.Count; i++) {
            if (updates[i].HasLocal && anchors[i] != null && claiming[anchors[i]] > 1) {
                updates[i].Selected = false;
            }
        }
    }

    /// remoteCell is the cell exactly as the script read it, never a parsed
    /// time: empty means the sheet holds nothing there, and anything that does
    /// not parse means it holds something unreadable. The two must not be
    /// confused. An unreadable cell used to count as empty, so the row ticked
    /// itself and overwrote whatever was in it; a sheet whose Google locale
    /// writes 8,704 rather than 8.704 does that on every single row, every time.
    public static PendingUpdate Create(SheetRowRef row, string label, long localTicks, string remoteCell,
        SheetSegment segment = null) {
        long? remoteTicks = SheetData.TryParseTime(remoteCell)?.Ticks;
        bool unreadable = remoteTicks == null && !string.IsNullOrWhiteSpace(remoteCell);
        // the screen lists the whole route, so most rows carry no run
        bool hasLocal = localTicks > 0;

        // an unreadable cell is never an improvement: we cannot tell, and the
        // safe default is to leave a time we do not understand alone
        bool improves = hasLocal && !unreadable && (remoteTicks == null || localTicks < remoteTicks.Value);

        string delta = "";
        if (!hasLocal) {
            delta = "";
        } else if (unreadable) {
            delta = "?";
        } else if (remoteTicks != null) {
            double seconds = TimeSpan.FromTicks(localTicks - remoteTicks.Value).TotalSeconds;
            delta = (seconds < 0 ? "-" : "+")
                  + Math.Abs(seconds).ToString("0.000", CultureInfo.InvariantCulture);
        }

        return new PendingUpdate {
            Row = row,
            Segment = segment,
            Label = label,
            LocalTicks = localTicks,
            RemoteTicks = remoteTicks,
            LocalText = hasLocal ? TimeFormat.FromTicks(localTicks) : "",
            // show the cell as it stands rather than nothing: the player is the
            // only one who can tell a locale from a typo from a note
            RemoteText = unreadable ? remoteCell.Trim()
                       : remoteTicks == null ? "" : TimeFormat.FromTicks(remoteTicks.Value),
            DeltaText = delta,
            WillImprove = improves,
            HasLocal = hasLocal,
            RemoteUnreadable = unreadable,
            Selected = improves,
        };
    }
}
