using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

public class RemoteBestsStateTests {
    [Fact]
    public void StartsNotLoaded() {
        RemoteBests.Reset();
        Assert.Equal(RemoteState.NotLoaded, RemoteBests.State);
    }

    [Fact]
    public void AcceptingRowsMovesToReadyAndIndexesThem() {
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "1a", Cp = "Crossing", Time = "21.948" },
        ]);

        Assert.Equal(RemoteState.Ready, RemoteBests.State);
        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "1a", "Crossing"), out RemoteRow row));
        Assert.Equal("21.948", row.Time);
    }

    [Fact]
    public void FailingMovesToErrorAndKeepsTheMessage() {
        RemoteBests.Reset();
        RemoteBests.Fail("boom");

        Assert.Equal(RemoteState.Error, RemoteBests.State);
        Assert.Equal("boom", RemoteBests.Error);
    }

    [Fact]
    public void LookupIsCaseAndEmojiInsensitiveOnTheCpLabel() {
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "6a", Cp = "Hollows \U0001F4FC", Time = "8.704" },
        ]);

        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "6a", "Hollows \U0001F4FC"), out _));
    }

    [Fact]
    public void LookupStripsVariationSelectorFromRawSheetEcho() {
        // Code.gs's doGet()/readTable() returns the RAW sheet cell text, which
        // may carry a trailing U+FE0F variation selector that SheetLabels.cs's
        // hardcoded emoji literal does not. The lookup key (built from
        // SheetLabels, no variation selector) must still find this row.
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "6a", Cp = "Hollows \U0001F4FC️", Time = "8.704" },
        ]);

        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "6a", "Hollows \U0001F4FC"), out _));
    }

    [Fact]
    public void RowsDifferingOnlyByEmojiStayDistinct() {
        // ten such pairs exist in the sheet ("0m" / "0m \U0001F48E", every 7A
        // checkpoint, "Crossing" / "Crossing \U0001F499"...). Stripping emoji
        // instead of only the variation selector merges them.
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "7a", Cp = "0m", Time = "39.457" },
            new RemoteRow { Tab = "A Sides", Chapter = "7a", Cp = "0m \U0001F48E", Time = "12.345" },
        ]);

        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "7a", "0m"), out RemoteRow plain));
        Assert.Equal("39.457", plain.Time);
        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "7a", "0m \U0001F48E"), out RemoteRow gem));
        Assert.Equal("12.345", gem.Time);
    }

    [Fact]
    public void UnknownRowIsNotFound() {
        RemoteBests.Reset();
        RemoteBests.Accept([]);
        Assert.False(RemoteBests.TryGet(new SheetRowRef("A Sides", "9z", "Nowhere"), out _));
    }
}
