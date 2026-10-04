using System;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// synthetic rows, never the fixtures: the case below is a stale WR, and a
// fixture refresh after it is beaten would make the test pin nothing
public class TierTests {
    private static readonly List<string> Columns = ["Hidden", "WR", "Gold", "Pink"];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Theory]
    [InlineData(48.0, "WR")]
    [InlineData(48.8, "WR")]
    // a time equal to the WR reaches it, as the sheet ranks
    [InlineData(49.011, "WR")]
    [InlineData(49.5, "Pink")]
    // any other tier needs a time strictly under its threshold
    [InlineData(50.0, "Unranked")]
    [InlineData(51.0, "Unranked")]
    public void TheFirstColumnInSheetOrderTheTimeBeatsWins(double seconds, string tier) {
        // a WR slower than Gold, as 5A Unravelling had it: 49.011 against 48.5
        List<TimeSpan?> thresholds = [S(0), S(49.011), S(48.5), S(50)];

        Assert.Equal(tier, SheetData.TierOf(Columns, thresholds, S(seconds)));
    }

    [Fact]
    public void ZeroAndEmptyThresholdsNeverMatch() {
        List<TimeSpan?> thresholds = [S(0), S(0), null, S(50)];

        Assert.Equal("Pink", SheetData.TierOf(Columns, thresholds, S(1)));
    }

    [Theory]
    [InlineData("Pink", "Gold", 45.0)]
    [InlineData("Gold", "WR", 40.0)]
    // Unranked aims at the slowest tier, and Hidden's zero is no target
    [InlineData("Unranked", "Pink", 50.0)]
    public void TheNextTierIsTheNearestFasterColumnWithAThreshold(string tier, string next, double seconds) {
        List<TimeSpan?> thresholds = [S(0), S(40), S(45), S(50)];

        Assert.Equal((next, S(seconds)), SheetData.NextTier(Columns, thresholds, tier));
    }

    [Fact]
    public void NothingIsFasterThanTheWR() {
        List<TimeSpan?> thresholds = [S(0), S(40), S(45), S(50)];

        Assert.Null(SheetData.NextTier(Columns, thresholds, "WR"));
    }

    [Fact]
    public void AnEmptyColumnIsSkippedForTheNextTier() {
        List<TimeSpan?> thresholds = [S(0), S(40), null, S(50)];

        Assert.Equal(("WR", S(40)), SheetData.NextTier(Columns, thresholds, "Pink"));
    }

    [Fact]
    public void BehindAStaleWRTheNextTierIsTheWR() {
        // a WR slower than Gold, as 5A Unravelling had it: from Pink, the WR
        // is the smaller gain
        List<TimeSpan?> thresholds = [S(0), S(49.011), S(48.5), S(50)];

        Assert.Equal(("WR", S(49.011)), SheetData.NextTier(Columns, thresholds, "Pink"));
    }
}
