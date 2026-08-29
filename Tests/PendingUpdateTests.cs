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
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "6b", "Falling"), "6b Falling",
            Ticks(67.915), "69.412");

        Assert.True(update.WillImprove);
        Assert.True(update.Selected);
        Assert.Equal("-1.497", update.DeltaText);
    }

    [Fact]
    public void ASlowerLocalTimeIsShownUnselected() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "6b", "6b Rock Bottom"), "6b Rock Bottom",
            Ticks(52.479), "51.980");

        Assert.False(update.WillImprove);
        Assert.False(update.Selected);
        Assert.Equal("+0.499", update.DeltaText);
    }

    [Fact]
    public void AnEmptyCellCountsAsAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), "");

        Assert.True(update.WillImprove);
        Assert.True(update.Selected);
        Assert.False(update.RemoteUnreadable);
        Assert.Equal("", update.RemoteText);
        Assert.Equal("", update.DeltaText);
    }

    [Fact]
    public void AnAbsentCellCountsAsAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), null);

        Assert.True(update.WillImprove);
        Assert.False(update.RemoteUnreadable);
    }

    [Fact]
    public void AnIdenticalTimeIsNotAnImprovement() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "1a", "Crossing"), "1a Crossing",
            Ticks(21.948), "21.948");

        Assert.False(update.WillImprove);
        Assert.Equal("+0.000", update.DeltaText);
    }

    // the sheet holds a time and this mod cannot read it. Treating that as an
    // empty cell ticked the row and overwrote it, and a sheet whose Google
    // locale writes a decimal comma does it on every row, every time
    [Theory]
    [InlineData("8,704")]   // a French locale, the case that made this a bug
    [InlineData("n/a")]
    [InlineData("see below")]
    public void AnUnreadableCellIsNeverAnImprovement(string cell) {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "5a", "Depths"), "5a Depths",
            Ticks(30.0), cell);

        Assert.True(update.RemoteUnreadable);
        Assert.False(update.WillImprove);
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

        Assert.False(update.RemoteUnreadable);
        Assert.True(update.WillImprove);
        Assert.Equal("", update.DeltaText);
    }

    [Fact]
    public void LocalTextUsesTheSpeedrunToolFormat() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "2a", "Awake"), "2a Awake",
            Ticks(14.722), null);

        Assert.Equal("14.722", update.LocalText);
    }
    // the cell is kept as the sheet displayed it, beside the parsed value. The
    // write compares against this one: the sheet writes some times short, and
    // "1:36.9" reformatted is "1:36.900", which would refuse the row
    [Fact]
    public void KeepsTheSheetCellAsItWasRead() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "2a", "Intervention"),
            "2a Intervention", Ticks(90.0), "1:36.9");

        Assert.Equal("1:36.9", update.RemoteCell);
        Assert.Equal("1:36.900", update.RemoteText);
    }

    [Fact]
    public void AnEmptyCellIsKeptAsAnEmptyString() {
        var update = PendingUpdate.Create(new SheetRowRef("A Sides", "7a", "3000m"), "7a 3000m",
            Ticks(41.5), null);

        Assert.Equal("", update.RemoteCell);
    }
}

public class SharedCheckpointTests {
    private static PendingUpdate Row(string label, long localTicks, string remoteCell) =>
        PendingUpdate.Create(new SheetRowRef("A Sides", "6a", label), label, localTicks, remoteCell);

    // the plain row and its cassette variant sit on the same game checkpoint, so
    // the held run answers to both. The variant's cell is usually empty, which
    // makes "it improves on the sheet" trivially true for a row never run
    [Fact]
    public void NothingIsTickedWhenTwoRowsShareACheckpoint() {
        List<PendingUpdate> updates = [
            Row("Hollows", 28_0000000L, "0:30.000"),
            Row("Hollows Tape", 28_0000000L, ""),
        ];
        Assert.All(updates, u => Assert.True(u.Selected));

        PendingUpdate.UntickSharedCheckpoints(updates, ["Hollows", "Hollows"]);

        Assert.All(updates, u => Assert.False(u.Selected));
    }

    // one row per checkpoint is the normal case, and it keeps its default
    [Fact]
    public void ARowAloneOnItsCheckpointKeepsItsTick() {
        List<PendingUpdate> updates = [
            Row("Hollows", 28_0000000L, "0:30.000"),
            Row("Lake", 12_0000000L, "0:13.000"),
        ];

        PendingUpdate.UntickSharedCheckpoints(updates, ["Hollows", "Lake"]);

        Assert.All(updates, u => Assert.True(u.Selected));
    }

    // rows carrying no run of their own are not what the rule is about, and a
    // checkpoint they share must not disarm the one row that does carry it
    [Fact]
    public void RowsWithoutARunDoNotCount() {
        List<PendingUpdate> updates = [
            Row("Hollows", 28_0000000L, "0:30.000"),
            Row("Hollows Tape", 0L, ""),
        ];

        PendingUpdate.UntickSharedCheckpoints(updates, ["Hollows", "Hollows"]);

        Assert.True(updates[0].Selected);
        Assert.False(updates[1].Selected);
    }
}
