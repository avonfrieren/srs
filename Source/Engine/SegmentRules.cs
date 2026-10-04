using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// The rule of every imported row, built once from SheetRows: static data, so
/// a sheet re-import never touches it.
internal static class SegmentRules {
    private const string Heart = "\U0001F499";
    private const string Tape = "\U0001F4FC";
    private const string Gem = "\U0001F48E";

    public static readonly IReadOnlyList<SegmentRule> All = Build(SheetRows.All);

    internal static List<SegmentRule> Build(IReadOnlyList<SheetRow> rows) {
        HashSet<(StandardsTab, string)> labels = [];
        foreach (SheetRow row in rows) {
            labels.Add((row.Tab, row.Label));
        }

        List<SegmentRule> rules = [];
        for (int i = 0; i < rows.Count; i++) {
            SheetRow row = rows[i];
            (string, string) anchor = (row.Scope, row.Anchor);
            Collectibles marked = MarkersOf(row.Label);
            EndKind end = EndOf(row.Label, marked);

            rules.Add(new SegmentRule(
                row.Scope, row.Chapter, row.Name, row.Anchor,
                SegmentAutoDetect.AfterLaunchStarts.Contains(anchor) ? StartKind.AfterLaunch : StartKind.Room,
                row.Anchor == "Start" || SegmentAutoDetect.CurrentRoomStarts.Contains(anchor)
                    ? StartSetup.CurrentRoom
                    : StartSetup.NextRoom,
                end,
                end == EndKind.Collect ? marked : Collectibles.None,
                marked,
                RequiresBerries: false,
                DashesOf(row, labels),
                SegmentAutoDetect.UntimedSegmentHead.GetValueOrDefault(anchor).Ticks,
                SegmentAutoDetect.UntimedSegmentTail.GetValueOrDefault(anchor).Ticks,
                i));
        }

        return rules;
    }

    // "RTM" and "RC" are the only suffixes that end a run before its segment
    // does: RTM at the heart or cassette it marks, RC at Restart Chapter, where
    // what it marks is a requirement. "Clear" means collect and keep going
    private static EndKind EndOf(string label, Collectibles marked) {
        string name = label.TrimEnd();
        if (name.EndsWith("RC", StringComparison.Ordinal)) {
            return EndKind.Restart;
        }

        return name.EndsWith("RTM", StringComparison.Ordinal)
               && (marked & (Collectibles.Heart | Collectibles.Cassette)) != Collectibles.None
            ? EndKind.Collect
            : EndKind.NextStart;
    }

    // matched with Contains: the sheet's spacing around a marker is irregular
    private static Collectibles MarkersOf(string label) =>
        (label.Contains(Heart) ? Collectibles.Heart : Collectibles.None)
        | (label.Contains(Tape) ? Collectibles.Cassette : Collectibles.None)
        | (label.Contains(Gem) ? Collectibles.Gem : Collectibles.None);

    // Farewell's DTS twins: "X DTS" keeps both dashes (2), "X" beside it lost one (1)
    private static int? DashesOf(SheetRow row, HashSet<(StandardsTab, string)> labels) {
        if (row.Tab != StandardsTab.Farewell) {
            return null;
        }

        if (row.Label.EndsWith(" DTS", StringComparison.Ordinal)) {
            return 2;
        }

        return labels.Contains((row.Tab, row.Label + " DTS")) ? 1 : null;
    }
}
