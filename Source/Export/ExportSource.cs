using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// Turns this session's bests into the export screen's rows, in the sheet's
/// order. Not SpeedrunTool's PbTimes: srs never sets NumberOfRooms, so those
/// are cut on the player's setting and describe no sheet segment. Mapped
/// through srs's own row table, so a session with no standards still exports.
internal static class ExportSource {
    public static List<PendingUpdate> Collect(IEnumerable<RunBook.Run> runs) {
        List<(int Order, PendingUpdate Update)> found = [];
        foreach (RunBook.Run run in runs) {
            if (!SheetRows.TryFind(run.Chapter, run.Name, out SheetRow sheetRow)) {
                continue;
            }

            SheetRowRef row = SheetRows.TargetOf(sheetRow);
            // the raw cell, not a parsed time: PendingUpdate has to tell an
            // empty cell from one it cannot read, and only the cell says which
            RemoteBests.TryGet(row, out RemoteRow remote);
            found.Add((Array.IndexOf(SheetRows.All, sheetRow), PendingUpdate.Create(row, DisplayName(run),
                run.Ticks, remote?.Time, remote?.Band ?? "", RemoteBests.IsDuplicate(row))));
        }

        found.Sort((a, b) => a.Order.CompareTo(b.Order));
        return found.ConvertAll(entry => entry.Update);
    }

    /// srs folds 6A and 6B into "6a/b" and re-prefixes the names both sides
    /// share ("6a Rock Bottom"). On screen that prefix is noise, and dropping it
    /// collides with nothing within a single side.
    private static string DisplayName(RunBook.Run run) =>
        run.Name.StartsWith(run.Scope + " ", StringComparison.Ordinal) ? run.Name[(run.Scope.Length + 1)..] : run.Name;
}
