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
    [InlineData(49.5, "Pink")]
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
}
