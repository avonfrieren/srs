using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

[Collection(RemoteBestsCollection.Name)]
public class ExportSourceTests {
    private static long Ticks(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    private static RunBook.Run Run(string scope, string chapter, string name, double seconds) =>
        new(scope, chapter, name, Ticks(seconds));

    // the sheet's order, whatever order the session ran them in, and without
    // the standards: the rows come from srs's own table
    [Fact]
    public void ListsEveryRunInTheSheetsOrder() {
        RemoteBests.Reset();
        List<PendingUpdate> updates = ExportSource.Collect([
            Run("2a", "2a", "Awake", 40), Run("1a", "1a", "Chasm", 30), Run("1a", "1a", "Start", 20),
        ]);

        Assert.Equal(["Start", "Chasm", "Awake"], updates.Select(u => u.Label));
        Assert.Equal(new SheetRowRef("A Sides", "1a", "1a Start"), updates[0].Row);
        // nothing held: every row is an improvement on an empty cell
        Assert.All(updates, u => Assert.True(u.Selected));
    }

    // a side's berry rows follow that side's own, before the next chapter,
    // under a header of their own
    [Fact]
    public void ListsASidesBerryRowsUnderThatSide() {
        RemoteBests.Reset();
        List<PendingUpdate> updates = ExportSource.Collect([
            Run("2a", "2a", "ARB IL", 150), Run("2a", "2a", "Awake", 40), Run("1a", "1a", "ARB Start", 35),
            Run("2a", "2a", "ARB Start", 70), Run("1a", "1a", "IL", 100),
        ]);

        Assert.Equal(["IL", "Start", "Awake", "Start", "IL"], updates.Select(u => u.Label));
        Assert.Equal(["1a", "1arb", "2a", "2arb", "2arb"], updates.Select(u => ExportTable.GroupLabel(u.Row)));
        Assert.Equal(new SheetRowRef("ARB/Full Clear", "2a \U0001F353", "2a \U0001F353"), updates[4].Row);
    }

    [Fact]
    public void ComparesAgainstTheSheetAndCarriesItsBand() {
        RemoteBests.Reset();
        RemoteBests.AcceptFresh([
            new RemoteRow { Tab = "A Sides", Band = "checkpoint", Chapter = "1a", Cp = "Crossing", Time = "21.948" },
        ]);
        PendingUpdate update = Assert.Single(ExportSource.Collect([Run("1a", "1a", "Crossing", 22.5)]));

        Assert.False(update.Selected);
        Assert.Equal("21.948", update.RemoteCell);
        Assert.Equal("checkpoint", update.Band);
    }

    [Fact]
    public void ADuplicateRowIsListedButNeverTicked() {
        RemoteBests.Reset();
        RemoteBests.AcceptFresh([
            new RemoteRow { Tab = "A Sides", Band = "checkpoint", Chapter = "1a", Cp = "Crossing", Time = "25.000" },
            new RemoteRow { Tab = "A Sides", Band = "il", Chapter = "1a", Cp = "Crossing", Time = "25.000" },
        ]);
        PendingUpdate update = Assert.Single(ExportSource.Collect([Run("1a", "1a", "Crossing", 20)]));

        Assert.True(update.Duplicate);
        Assert.False(update.Selected);
    }

    [Fact]
    public void ARunTheTableDoesNotHoldIsLeftOut() {
        RemoteBests.Reset();
        Assert.Empty(ExportSource.Collect([Run("1a", "1a", "Nowhere", 20)]));
    }

    // one chapter cell holds a checkpoint row and the chapter row: two cells, two bands
    [Fact]
    public void ABerryChapterRowIsComparedInItsOwnBand() {
        const string cell = "2a \U0001F353";
        RemoteBests.Reset();
        RemoteBests.AcceptFresh([
            new RemoteRow { Tab = "ARB/Full Clear", Band = "checkpoint", Chapter = cell, Cp = "2a Start", Time = "1:05.000" },
            new RemoteRow { Tab = "ARB/Full Clear", Band = "il", Chapter = cell, Cp = cell, Time = "2:50.000" },
        ]);
        List<PendingUpdate> updates = ExportSource.Collect([
            Run("2a", "2a", "ARB IL", 165), Run("2a", "2a", "ARB Start", 64),
        ]);

        Assert.Equal(["Start", "IL"], updates.Select(u => u.Label));
        Assert.Equal(["checkpoint", "il"], updates.Select(u => u.Band));
        Assert.Equal(["1:05.000", "2:50.000"], updates.Select(u => u.RemoteCell));
        Assert.Equal(new SheetRowRef("ARB/Full Clear", cell, cell), updates[1].Row);
        Assert.All(updates, u => Assert.False(u.Duplicate));
    }
}
