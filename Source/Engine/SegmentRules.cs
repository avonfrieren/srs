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

    /// The rule of srs's (chapter, name), or null.
    public static SegmentRule Find(string chapter, string name) {
        foreach (SegmentRule rule in All) {
            if (rule.Chapter == chapter && rule.Name == name) {
                return rule;
            }
        }

        return null;
    }

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
            // RTM and RC are the only markers that end a run early, at the collect
            bool endsAtCollect = SheetData.EndConditionOf(row.Label) != EndCondition.Checkpoint;

            rules.Add(new SegmentRule(
                row.Scope, row.Chapter, row.Name, row.Anchor,
                SegmentAutoDetect.AfterLaunchStarts.Contains(anchor) ? StartKind.AfterLaunch : StartKind.Room,
                endsAtCollect ? EndKind.Collect : EndKind.NextStart,
                endsAtCollect ? marked : Collectibles.None,
                marked,
                RequiresBerries: false,
                DashesOf(row, labels),
                SegmentAutoDetect.UntimedSegmentHead.GetValueOrDefault(anchor).Ticks,
                SegmentAutoDetect.UntimedSegmentTail.GetValueOrDefault(anchor).Ticks,
                i));
        }

        return rules;
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
