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

    [Fact]
    public void ShortensEachLabelByItsOwnSide() {
        RemoteBests.Reset();
        List<PendingUpdate> updates = ExportSource.Collect([
            Run("6b", "6a/b", "6b Rock Bottom", 50), Run("6a", "6a/b", "6a Start", 20),
        ]);

        Assert.Equal(["Start", "Rock Bottom"], updates.Select(u => u.Label));
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
}
