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
    public void ANonPositiveTimeIsNeverKept() {
        RunBook book = new();
        Assert.Empty(book.Offer([new SegmentRecord(TestRules.Find("6a/b", "Hollows"), 0)]));

        Assert.Empty(book.All);
    }

    // a session's bests outlive the chapter: an export from 2a carries 1a's too
    [Fact]
    public void KeepsTheRowsOfEveryChapter() {
        RunBook book = new();
        book.Offer([Record("1a", "Start", 30.5)]);
        book.Offer([Record("2a", "Start", 61)]);

        Assert.Equal(2, book.All.Count);
    }
}
