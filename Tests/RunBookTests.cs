using System;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the session's bests, one per row: what the export writes
public class RunBookTests {
    private static long Ticks(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    private static SegmentRecord Record(string chapter, string name, double seconds) =>
        new(TestRules.Find(chapter, name), Ticks(seconds));

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
        Assert.Empty(book.Offer([new SegmentRecord(TestRules.Find("6a/b", "Hollows"), 0)]));

        Assert.Null(book.LastImproved);
    }

    // a chapter's last segment closes as its ending plays, so the book must
    // outlive the level: only another chapter, or one the sheet does not
    // cover, drops it
    [Fact]
    public void OnlyAnotherChapterDropsTheBook() {
        RunBook book = new();
        book.Offer([Record("3a", "Presidential Suite", 80)]);

        Assert.False(book.DropUnlessIn("3a"));
        Assert.Single(book.All);
        Assert.True(book.DropUnlessIn("4a"));
        Assert.Empty(book.All);

        book.Offer([Record("3a", "Presidential Suite", 80)]);
        Assert.True(book.DropUnlessIn(null));
        Assert.False(book.DropUnlessIn("3a"));
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
}
