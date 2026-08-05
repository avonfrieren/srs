using System;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// SheetData.Parse promises never to throw on malformed content: it runs at
// game startup on whatever sits in the cache, so a broken or truncated CSV has
// to degrade into "no segments" instead of taking Celeste down with it
public class MalformedInputTests {
    private const string Header = "Chapter,Checkpoint,Hidden,WR,Gold\n";

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "\n\n")]
    [InlineData("no,header,here\n1,2,3", "")]
    [InlineData(",,,,\n,,,,", ",,,")]
    public void ReturnsNoSegmentsInsteadOfThrowing(string aSides, string bSides) {
        SheetData data = SheetData.Parse(aSides, bSides);

        Assert.Equal(0, data.SegmentCount);
    }

    // a sheet that stopped being shared publicly answers 200 with a sign-in
    // page; the importer rejects it, but the parser must not choke on it either
    [Fact]
    public void SurvivesAnHtmlErrorPage() {
        SheetData data = SheetData.Parse("<!DOCTYPE html><html><body>Sign in</body></html>", null);

        Assert.Equal(0, data.SegmentCount);
    }

    [Fact]
    public void SurvivesRowsCutShortOfTheHeader() {
        SheetData data = SheetData.Parse(Header + "1a CP,1a Start,0:00.000", null);

        SheetSegment segment = Assert.Single(data.CheckpointBlock.Segments);
        // one time per declared tier column, the missing cells reading as null
        Assert.Equal(3, segment.Times.Count);
        Assert.Equal(TimeSpan.Zero, segment.Times[0]);
        Assert.Null(segment.Times[1]);
        Assert.Null(segment.Times[2]);
    }

    [Fact]
    public void TurnsBrokenFormulaCellsIntoNullThresholds() {
        SheetData data = SheetData.Parse(Header + "1a CP,1a Start,0:00.000,#REF!,#REF!", null);

        SheetSegment segment = Assert.Single(data.CheckpointBlock.Segments);
        Assert.Null(segment.Times[1]);
        Assert.Null(segment.Times[2]);
    }

    // either tab may be missing (first run, half-written cache, one download
    // that failed): whatever is there still has to load
    [Fact]
    public void ParsesTheASidesTabOnItsOwn() {
        SheetData data = SheetData.Parse(Fixtures.ASides, null);

        Assert.NotEmpty(data.CheckpointBlock.Segments);
        Assert.DoesNotContain(data.CheckpointBlock.Segments, s => s.Name == "5b Start");
    }

    [Fact]
    public void ParsesTheBSidesTabOnItsOwn() {
        SheetData data = SheetData.Parse(null, Fixtures.BSides);

        Assert.NotEmpty(data.CheckpointBlock.Segments);
        Assert.Contains(data.CheckpointBlock.Segments, s => s.Name == "5b Start");
        Assert.DoesNotContain(data.CheckpointBlock.Segments, s => s.Name == "Granny");
    }

    [Fact]
    public void HasNoCheckpointBlockWhenThereIsNothingToParse() {
        SheetData data = SheetData.Parse(null, null);

        Assert.Null(data.CheckpointBlock);
    }

    // the cache is read with File.ReadAllText (which strips the BOM) but the
    // download is decoded straight from the response, so a byte-order mark can
    // reach the parser
    [Fact]
    public void SurvivesAByteOrderMark() {
        SheetData data = SheetData.Parse("﻿" + Header + "1a CP,1a Start,0:00.000,13.906,14.5", null);

        Assert.Equal(1, data.SegmentCount);
    }

    [Fact]
    public void SkipsRowsThatAreNotOnTheImportList() {
        // emoji variants and IL rows sit right next to the real checkpoints
        SheetData data = SheetData.Parse(
            Header + "1a CP,1a Start,0:00.000,13.906,14.5\n,Crossing 💙,0:00.000,0:00.000,\n1a IL,Clear,0:00.000,52.445,55.5", null);

        SheetSegment segment = Assert.Single(data.CheckpointBlock.Segments);
        Assert.Equal("Start", segment.Name);
    }
}

// the two tabs as they were exported on 2026-08-05; refreshing them is how a
// change in the sheet becomes a failing test (see SheetConsistencyTests)
internal static class Fixtures {
    public static string ASides { get; } = Read("asides.csv");
    public static string BSides { get; } = Read("bsides.csv");

    public static SheetData Parsed { get; } = SheetData.Parse(ASides, BSides);

    public static List<SheetSegment> Imported => Parsed.CheckpointBlock.Segments;

    private static string Read(string name) =>
        System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
