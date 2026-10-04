using System.Collections.Generic;
using System;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the remote side is passed as the cell the script read, never as a parsed
// time: telling an empty cell from an unreadable one is the whole point, and a
// caller that parses first has already lost the distinction
public class PendingUpdateTests {
    private static long Ticks(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    [Fact]
    public void AFasterLocalTimeIsAnImprovementAndIsPreselected() {
        var update = PendingUpdate.Create(new SheetRowRef("B+C Sides", "6b", "Falling"), "6b Falling",
            Ticks(67.915), "69.412");

        Assert.True(update.Selected);
        Assert.Equal("-1.497", update.DeltaText);
    }

    [Fact]
    public void ASlowerLocalTimeIsShownUnselected() {
        var update = PendingUpdate.Create(new SheetRowRef("B+C Sides", "6b", "Rock Bottom"), "6b Rock Bottom",
            Ticks(52.479), "51.980");

        Assert.False(update.Selected);
        Assert.Equal("+0.499", update.DeltaText);
    }

    [Fact]
    public void AnEmptyCellCountsAsAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), "");

        Assert.True(update.Selected);
        Assert.Equal("", update.RemoteText);
        Assert.Equal("", update.DeltaText);
    }

    [Fact]
    public void AnAbsentCellCountsAsAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), null);

        Assert.True(update.Selected);
        Assert.Equal("", update.DeltaText);
    }

    // the sheet writes 0:00.000 into a cell that has never held a time: its own
    // placeholder for "no record yet", not a PB to beat
    [Fact]
    public void AZeroCellCountsAsAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), "0:00.000");

        Assert.True(update.Selected);
        Assert.Null(update.RemoteTicks);
        Assert.Equal("", update.RemoteText);
        Assert.Equal("", update.DeltaText);
        // the raw cell is still what the write compares against
        Assert.Equal("0:00.000", update.RemoteCell);
    }

    [Fact]
    public void AnIdenticalTimeIsNotAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "1a Crossing",
            Ticks(21.948), "21.948");

        Assert.False(update.Selected);
        Assert.Equal("+0.000", update.DeltaText);
    }

    // the sheet holds a time and this mod cannot read it. Treating that as an
    // empty cell ticked the row and overwrote it, and a sheet whose Google
    // locale writes a decimal comma does it on every row, every time
    [Theory]
    [InlineData("8,704")]   // a French locale, the case that made this a bug
    [InlineData("n/a")]
    [InlineData("see below")]
    // an hour or more, which Speed Run Tool's format would show an hour short
    [InlineData("1:05:03")]
    public void AnUnreadableCellIsNeverAnImprovement(string cell) {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "5a", "Depths"), "5a Depths",
            Ticks(30.0), cell);

        Assert.False(update.Selected);
        Assert.Null(update.RemoteTicks);
        // shown as it stands: only the player can tell a locale from a typo
        Assert.Equal(cell, update.RemoteText);
        Assert.Equal("?", update.DeltaText);
    }

    [Fact]
    public void AWhitespaceOnlyCellIsEmptyRatherThanUnreadable() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "5a", "Search"), "5a Search",
            Ticks(30.0), "   ");

        Assert.True(update.Selected);
        Assert.Equal("", update.DeltaText);
    }

    [Fact]
    // what the format looks like is SpeedrunTool's business and only the
    // stand-in's here: assert the route, never the string
    public void LocalTextGoesThroughTheTimeFormat() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "2a", "Awake"), "2a Awake",
            Ticks(14.722), null);

        Assert.Equal(TimeFormat.FromTicks(Ticks(14.722)), update.LocalText);
    }
    // the cell is kept as the sheet displayed it, beside the parsed value. The
    // write compares against this one: the sheet writes some times short, and
    // "1:36.9" reformatted is "1:36.900", which would refuse the row
    [Fact]
    public void KeepsTheSheetCellAsItWasRead() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "2a", "Intervention"),
            "2a Intervention", Ticks(90.0), "1:36.9");

        Assert.Equal("1:36.9", update.RemoteCell);
        Assert.Equal(TimeFormat.FromTicks(TimeSpan.FromMilliseconds(96_900).Ticks), update.RemoteText);
    }

    [Fact]
    public void AnEmptyCellIsKeptAsAnEmptyString() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), null);

        Assert.Equal("", update.RemoteCell);
    }
}
