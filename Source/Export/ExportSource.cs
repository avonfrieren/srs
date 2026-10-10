using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Turns this session's bests into the export screen's rows, in the sheet's
/// order, a side's berry rows right under that side's own. Not SpeedrunTool's PbTimes, which are cut on the player's setting
/// and describe no sheet segment. Mapped through srs's own row table, so a
/// session with no standards still exports.
internal static class ExportSource {
    public static List<PendingUpdate> Collect(IEnumerable<RunBook.Run> runs) {
        List<((int Side, int Row) Order, PendingUpdate Update)> found = [];
        foreach (RunBook.Run run in runs) {
            if (!SheetRows.TryFind(run.Chapter, run.Name, out SheetRow sheetRow)) {
                continue;
            }

            SheetRowRef row = SheetRows.TargetOf(sheetRow);
            // the raw cell, not a parsed time: PendingUpdate has to tell an
            // empty cell from one it cannot read, and only the cell says which
            RemoteBests.TryGet(row, out RemoteRow remote);
            int side = Array.FindIndex(SheetRows.All, other => other.Scope == sheetRow.Scope);
            found.Add(((side, Array.IndexOf(SheetRows.All, sheetRow)), PendingUpdate.Create(row, DisplayName(run),
                run.Ticks, remote?.Time, remote?.Band ?? "", RemoteBests.IsDuplicate(row))));
        }

        found.Sort((a, b) => a.Order.CompareTo(b.Order));
        return found.ConvertAll(entry => entry.Update);
    }

    /// srs folds 6A and 6B into "6a/b" and re-prefixes the names both sides
    /// share ("6a Rock Bottom"), and names a berry row "ARB Start". On screen
    /// the group header says both, and dropping them collides with nothing
    /// within a group.
    private static string DisplayName(RunBook.Run run) {
        foreach (string prefix in (string[])[run.Scope + " ", "ARB "]) {
            if (run.Name.StartsWith(prefix, StringComparison.Ordinal)) {
                return run.Name[prefix.Length..];
            }
        }

        return run.Name;
    }
}
