using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// What the tier rows say: the time and its tier, the gain on the player's
/// sheet time when it is a PB, and the delta to the next tier. Game-free, so
/// tested.
internal readonly record struct TierRows(string Time, string Tier, string Pb, string Gap);

internal static class TierLine {
    /// `sheetTicks` is the player's own time for the row, null when unknown.
    /// The time is ranked as Speed Run Tool shows it, to the millisecond, which
    /// is also the time the export writes and the sheet ranks.
    public static TierRows Build(List<string> columns, List<TimeSpan?> thresholds, long ticks, long? sheetTicks) {
        string shownText = TimeFormat.FromTicks(ticks);
        TimeSpan shown = SheetData.TryParseTime(shownText) ?? TimeSpan.FromTicks(ticks);
        string tier = SheetData.TierOf(columns, thresholds, shown);

        string pb = sheetTicks is { } sheet && shown.Ticks < sheet ? $"PB {Signed(shown.Ticks - sheet)}" : null;

        string gap = SheetData.NextTier(columns, thresholds, tier) is { } next
            ? $"{Signed(shown.Ticks - next.Threshold.Ticks)} to {next.Column}"
            : null;
        return new TierRows(shownText, tier, pb, gap);
    }

    /// "1a Crossing", "6a Lake": the chapter, unless the name is it ("1c",
    /// Farewell's "Farewell").
    public static string NameOf(string scope, string name) =>
        name == scope ? name : $"{scope} {name}";

    // Speed Run Tool's format with a sign, so a gap of a minute reads 1:02.345
    private static string Signed(long ticks) =>
        (ticks < 0 ? "-" : "+") + TimeFormat.FromTicks(Math.Abs(ticks));
}
