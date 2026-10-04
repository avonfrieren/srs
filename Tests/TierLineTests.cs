using System;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the time format is the stand-in's, so these assert what surrounds it: the
// tier, the tier a gap names, its side, and whether each row is there
public class TierLineTests {
    private static readonly List<string> Columns = ["Hidden", "WR", "Gold", "Pink", "Unranked"];
    private static readonly List<TimeSpan?> Thresholds = [S(0), S(40), S(45), S(50), null];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);
    private static long T(double seconds) => S(seconds).Ticks;

    private static TierRows Build(double time, double? sheet = null) =>
        TierLine.Build(Columns, Thresholds, T(time), sheet is { } s ? T(s) : null);

    [Fact]
    public void TheGapAimsAtTheNextTierBehind() {
        TierRows rows = Build(47);

        Assert.StartsWith("+", rows.Gap);
        Assert.EndsWith(" to Gold", rows.Gap);
        Assert.False(rows.GapAhead);
    }

    [Fact]
    public void UnderTheWRTheGapIsTheMarginAhead() {
        TierRows rows = Build(39);

        Assert.Equal("WR", rows.Tier);
        Assert.StartsWith("-", rows.Gap);
        Assert.EndsWith(" to WR", rows.Gap);
        Assert.True(rows.GapAhead);
    }

    [Fact]
    public void ATieWithTheWRIsAheadByNothing() {
        TierRows rows = Build(40);

        Assert.Equal("WR", rows.Tier);
        Assert.StartsWith("+", rows.Gap);
        Assert.True(rows.GapAhead);
    }

    [Theory]
    // faster than the sheet: a PB, with a negative gain
    [InlineData(47.0, 48.0, true)]
    // equal or slower: none
    [InlineData(48.0, 48.0, false)]
    [InlineData(49.0, 48.0, false)]
    public void APBIsATimeUnderTheSheetTime(double time, double sheet, bool pb) {
        TierRows rows = Build(time, sheet);

        Assert.Equal(pb, rows.Pb != null);
        if (pb) {
            Assert.StartsWith("PB -", rows.Pb);
        }
    }

    [Fact]
    public void NoSheetTimeNoPB() {
        Assert.Null(Build(47).Pb);
    }

    [Fact]
    public void TheTimeIsRankedAsShown() {
        // 40.0004 shows as 40.000, the WR exactly: a WR, as the sheet ranks the
        // exported time
        Assert.Equal("WR", Build(40.0004).Tier);
    }

    [Theory]
    [InlineData("1a", "Crossing", "1a Crossing")]
    // the name already carries its chapter, or is it
    [InlineData("6a", "6a Start", "6a Start")]
    [InlineData("Farewell", "Farewell", "Farewell")]
    public void TheNameLeadsWithItsChapterOnce(string scope, string name, string shown) {
        Assert.Equal(shown, TierLine.NameOf(scope, name));
    }
}
