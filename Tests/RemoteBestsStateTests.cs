using System;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// RemoteBests is static and xUnit runs test classes in parallel: no other
// class may touch it
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

    // a screen asking afresh: nothing is compared against a sheet that may have
    // moved, and nothing held has an age
    [Fact]
    public void BeginFetchEmptiesTheIndexAndWaits() {
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "1a", Cp = "Crossing", Time = "21.948" },
        ]);
        RemoteBests.BeginFetch();

        Assert.Equal(RemoteState.Loading, RemoteBests.State);
        Assert.False(RemoteBests.IsResolved);
        Assert.False(RemoteBests.TryGet(new SheetRowRef("A Sides", "1a", "Crossing"), out _));
        Assert.Equal(TimeSpan.MaxValue, RemoteBests.Age);
    }

    [Fact]
    public void AnAnswerIsAsOldAsItsArrival() {
        RemoteBests.Reset();
        Assert.Equal(TimeSpan.MaxValue, RemoteBests.Age);

        RemoteBests.Accept([]);
        Assert.True(RemoteBests.Age < TimeSpan.FromMinutes(1));
    }

    // a refresh failing under an open screen keeps the rows it was built from
    [Fact]
    public void FailingAfterAnAnswerKeepsItsRows() {
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "1a", Cp = "Crossing", Time = "21.948" },
        ]);
        RemoteBests.Fail("timeout");

        Assert.Equal(RemoteState.Error, RemoteBests.State);
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
    public void LookupIgnoresCaseAndSpacingButNotTheEmoji() {
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "a sides", Chapter = "6A", Cp = "  HOLLOWS   \U0001F4FC ", Time = "8.704" },
        ]);

        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "6a", "Hollows \U0001F4FC"), out _));
        Assert.False(RemoteBests.TryGet(new SheetRowRef("A Sides", "6a", "Hollows"), out _));
    }

    [Fact]
    public void LookupStripsVariationSelectorFromRawSheetEcho() {
        // The Web App's doGet()/readTable() returns the RAW sheet cell text, which
        // may carry a trailing U+FE0F variation selector that SheetLabels.cs's
        // hardcoded emoji literal does not. The lookup key (built from
        // SheetLabels, no variation selector) must still find this row.
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "6a", Cp = "Hollows \U0001F4FC\uFE0F", Time = "8.704" },
        ]);

        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "6a", "Hollows \U0001F4FC"), out _));
    }

    [Fact]
    public void RowsDifferingOnlyByEmojiStayDistinct() {
        // the sheet has such pairs ("7a Start" / "7a Start \U0001F48E", "Crossing" /
        // "Crossing \U0001F499"). Stripping emoji instead of only the variation
        // selector merges them.
        RemoteBests.Reset();
        RemoteBests.Accept([
            new RemoteRow { Tab = "A Sides", Chapter = "7a", Cp = "7a Start", Time = "39.457" },
            new RemoteRow { Tab = "A Sides", Chapter = "7a", Cp = "7a Start \U0001F48E", Time = "12.345" },
        ]);

        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "7a", "7a Start"), out RemoteRow plain));
        Assert.Equal("39.457", plain.Time);
        Assert.True(RemoteBests.TryGet(new SheetRowRef("A Sides", "7a", "7a Start \U0001F48E"), out RemoteRow gem));
        Assert.Equal("12.345", gem.Time);
    }

    [Fact]
    public void UnknownRowIsNotFound() {
        RemoteBests.Reset();
        RemoteBests.Accept([]);
        Assert.False(RemoteBests.TryGet(new SheetRowRef("A Sides", "9z", "Nowhere"), out _));
    }
}
