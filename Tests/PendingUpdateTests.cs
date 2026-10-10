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
        Assert.Equal("", update.RemoteCell);
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

    // the sheet holds a time and this mod cannot read it. Treated as an empty
    // cell, the row would be ticked and overwritten, and a sheet whose Google
    // locale writes a decimal comma would do it on every row, every time
    [Theory]
    [InlineData("8,704")]   // a French locale's decimal comma
    [InlineData("n/a")]
    [InlineData("see below")]
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

    // "65:03.250" is the same hour written as the standards write theirs
    [Theory]
    [InlineData("1:05:03.250")]
    [InlineData("65:03.250")]
    public void ACellOfAnHourOrMoreIsCompared(string cell) {
        var update = PendingUpdate.Create(new SheetRowRef("Farewell", "", "DTS IL"), "DTS IL",
            Ticks(3900.0), cell);

        Assert.Equal(Ticks(3903.25), update.RemoteTicks);
        Assert.True(update.Selected);
        Assert.Equal("1:05:00.000", update.LocalText);
    }

    // what the format looks like is Speed Run Tool's business and only the
    // stand-in's here: assert the route, never the string
    [Fact]
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
    }

    // the sheet and its script compare at the millisecond: a run a fraction
    // over the cell srs just wrote is the same time, not a regression
    [Fact]
    public void ATimeWrittenAtTheMillisecondIsNotBehind() {
        long ticks = TimeSpan.FromSeconds(21.948).Ticks + 4000;   // 21.9484 s
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing", ticks, "21.948");

        Assert.False(update.Selected);
        Assert.Equal("+0.000", update.DeltaText);
        Assert.Null(update.Ahead);
    }

    [Fact]
    public void AheadSaysWhichWayTheDeltaGoes() {
        Assert.True(PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing", Ticks(21.0), "21.948").Ahead);
        Assert.False(PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing", Ticks(22.0), "21.948").Ahead);
        Assert.Null(PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing", Ticks(22.0), "").Ahead);
        Assert.Null(PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing", Ticks(22.0), "8,704").Ahead);
    }

    [Fact]
    public void ADuplicateIsNeverTickedAndSaysWhy() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing", Ticks(21.0), "",
            duplicate: true);

        Assert.True(update.Duplicate);
        Assert.False(update.Selected);
        Assert.Equal("SRS_EXPORT_DUPLICATE", update.DeltaText);
        Assert.Equal("", update.RemoteText);
        Assert.Null(update.Ahead);
    }

    [Fact]
    public void KeepsTheBandItWasReadFrom() {
        Assert.Equal("checkpoint", PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "Crossing",
            Ticks(21.0), "21.948", band: "checkpoint").Band);
    }
}
