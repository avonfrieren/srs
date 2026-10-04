using System;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the session's bests, one per row: what the export writes
public class RunBookTests {
    private static long Ticks(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    private static SegmentRecord Record(string chapter, string name, double seconds) =>
        new(SegmentRules.Find(chapter, name), Ticks(seconds));

    [Fact]
    public void KeepsTheBestOfEachRow() {
        RunBook book = new();
        Assert.Single(book.Offer([Record("6a/b", "Hollows", 45)]));
        Assert.Empty(book.Offer([Record("6a/b", "Hollows", 47)]));
        Assert.Single(book.Offer([Record("6a/b", "Hollows", 44)]));

        Assert.Equal(Ticks(44), Assert.Single(book.All).Ticks);
    }

    // a cassette run never stands for the plain segment, and neither replaces the other
    [Fact]
    public void EveryRowKeepsItsOwnBest() {
        RunBook book = new();
        book.Offer([Record("6a/b", "Hollows Tape", 20)]);
        book.Offer([Record("6a/b", "Hollows", 45)]);

        Assert.Equal(2, book.All.Count);
    }

    [Fact]
    public void LastImprovedIsTheRowWhoseBestImprovedLast() {
        RunBook book = new();
        book.Offer([Record("1a", "Crossing", 30.5)]);
        book.Offer([Record("1a", "Start", 20)]);
        book.Offer([Record("1a", "Crossing", 31.2)]);

        Assert.Equal("Start", book.LastImproved?.Name);
    }

    [Fact]
    public void ATieOnOneFrameGoesToTheMostSpecific() {
        RunBook book = new();
        book.Offer([Record("3a", "Huge Mess", 60), Record("3a", "Huge Mess Heart", 60)]);

        Assert.Equal("Huge Mess Heart", book.LastImproved?.Name);
    }

    [Fact]
    public void ANonPositiveTimeIsNeverKept() {
        RunBook book = new();
        Assert.Empty(book.Offer([new SegmentRecord(SegmentRules.Find("6a/b", "Hollows"), 0)]));

        Assert.Null(book.LastImproved);
    }

    [Fact]
    public void ClearForgetsEverything() {
        RunBook book = new();
        book.Offer([Record("6a/b", "Hollows", 45)]);
        book.Clear();

        Assert.Empty(book.All);
        Assert.Null(book.LastImproved);
        Assert.Null(book.Scope);
    }

    // "Start" exists in nearly every chapter: the chapter is part of the address
    [Fact]
    public void FindAddressesASegmentByChapterAndName() {
        SheetBlock block = Fixtures.Parsed.CheckpointBlock;

        Assert.Equal("2a", block.Find("2a", "Start")?.Chapter);
        Assert.Null(block.Find("2a", "Hollows"));
    }
}
