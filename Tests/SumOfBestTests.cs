using System;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// The total under the export table. The base is the sheet's own chapter total,
// read from the summary block of the category tab, never a sum done here: the
// sheet counts rows srs does not import and reuses one block's first row in the
// next, so an addition would disagree with the document it claims to total.
public class SumOfBestTests {
    private static long Ticks(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    private static PendingUpdate Row(string cp, double local, string remoteCell) =>
        PendingUpdate.Create(new SheetRowRef("A Sides", "1a", cp), cp,
            local > 0 ? Ticks(local) : 0L, remoteCell);

    [Fact]
    public void TakesTheChapterTotalFromTheSheet() {
        var sum = SumOfBest.Of([Row("Start", 0, "42.000")], "1:36.832");

        Assert.True(sum.Known);
        Assert.Equal(TimeSpan.Parse("00:01:36.832").Ticks, sum.SheetTicks);
        // nothing ticked: the projection is the sheet as it stands
        Assert.Equal(sum.SheetTicks, sum.ProjectedTicks);
    }

    // the base is never re-derived from the rows on screen: they are a subset of
    // what the sheet totals, and this is the assertion that says so
    [Fact]
    public void IgnoresWhatTheRowsWouldAddUpTo() {
        var sum = SumOfBest.Of([Row("Start", 0, "10.000"), Row("Crossing", 0, "10.000")], "55.114");

        Assert.Equal(TimeSpan.Parse("00:00:55.114").Ticks, sum.SheetTicks);
    }

    [Fact]
    public void ATickedImprovementTakesItsSavingOffTheSheetTotal() {
        var sum = SumOfBest.Of([Row("Start", 40.0, "42.000")], "1:00.000");

        Assert.Equal(Ticks(58), sum.ProjectedTicks);
    }

    // a row the player left unticked is the sheet's, however fast the run was
    [Fact]
    public void AnUntickedRunDoesNotMoveTheTotal() {
        List<PendingUpdate> rows = [Row("Start", 40.0, "42.000")];
        rows[0].Selected = false;

        var sum = SumOfBest.Of(rows, "1:00.000");

        Assert.Equal(sum.SheetTicks, sum.ProjectedTicks);
    }

    // the sheet counts an empty cell as nothing, so filling one grows the total
    [Fact]
    public void TickingAnEmptyCellAddsItsWholeTime() {
        var sum = SumOfBest.Of([Row("Crossing", 30.0, "")], "1:00.000");

        Assert.Equal(Ticks(90), sum.ProjectedTicks);
    }

    // an unreadable cell is the one case where the saving cannot be worked out:
    // the sheet summed something we cannot read, so no projection is offered.
    // Such a row never ticks itself, so this is the player having ticked it
    [Fact]
    public void ATickedRowWithAnUnreadableCellLeavesNoProjection() {
        List<PendingUpdate> rows = [Row("Start", 8.0, "8,704")];
        rows[0].Selected = true;

        var sum = SumOfBest.Of(rows, "1:00.000");

        Assert.True(sum.Known);
        Assert.Null(sum.ProjectedTicks);
    }

    // while the fetch is in flight, on a route the sheet is not set to, and on a
    // chapter the sheet declines to total: all three arrive here as no cell
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#REF!")]
    [InlineData("0:00.000")]
    public void NoUsableCellIsNoTotal(string cell) {
        var sum = SumOfBest.Of([Row("Start", 40.0, "42.000")], cell);

        Assert.False(sum.Known);
        Assert.Null(sum.ProjectedTicks);
    }
}
