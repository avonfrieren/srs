using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// the export writes whatever HeldRun holds into the row of the segment it
// names, so a time of one segment held under another is a wrong time written
// into the player's sheet
public class HeldRunTests {
    private static long Ticks(double seconds) => TimeSpan.FromSeconds(seconds).Ticks;

    [Fact]
    public void ACassetteRunNeverStandsForThePlainSegment() {
        HeldRun held = new();
        held.Offer("6a", "6a/b", "Hollows Tape", Ticks(20));
        held.Offer("6a", "6a/b", "Hollows", Ticks(45));

        Assert.Equal(new HeldRun.Run("6a", "6a/b", "Hollows", Ticks(45)), held.Current);
    }

    [Fact]
    public void KeepsTheBestOfOneSegment() {
        HeldRun held = new();
        Assert.True(held.Offer("6a", "6a/b", "Hollows", Ticks(45)));
        Assert.False(held.Offer("6a", "6a/b", "Hollows", Ticks(47)));
        Assert.True(held.Offer("6a", "6a/b", "Hollows", Ticks(44)));

        Assert.Equal(Ticks(44), held.Current?.Ticks);
    }

    // one segment per export: going back to a segment run earlier starts over
    [Fact]
    public void AnotherSegmentReplacesEvenWhenSlower() {
        HeldRun held = new();
        held.Offer("6a", "6a/b", "Hollows", Ticks(45));
        held.Offer("6a", "6a/b", "Hollows Tape", Ticks(20));
        held.Offer("6a", "6a/b", "Hollows", Ticks(46));

        Assert.Equal(new HeldRun.Run("6a", "6a/b", "Hollows", Ticks(46)), held.Current);
    }

    [Fact]
    public void ANonPositiveTimeIsNeverHeld() {
        HeldRun held = new();
        Assert.False(held.Offer("6a", "6a/b", "Hollows", 0));

        Assert.Null(held.Current);
    }

    [Fact]
    public void TheArrowsNeverCrossFromACheckpointEndToACassetteEnd() {
        SheetSegment hollows = Imported("6a/b", "Hollows");

        List<string> names = HeldRun.CandidatesAmong(Fixtures.Imported, hollows, "6a").Select(s => s.Name).ToList();

        Assert.Equal(["Hollows"], names);
        Assert.Equal(["Hollows Tape"],
            HeldRun.CandidatesAmong(Fixtures.Imported, Imported("6a/b", "Hollows Tape"), "6a").Select(s => s.Name));
    }

    // the case the arrows exist for: a heart-route run detected as Any%
    [Fact]
    public void TheArrowsReachTheHeartRouteOfTheSameCheckpoint() {
        List<string> names = HeldRun.CandidatesAmong(Fixtures.Imported, Imported("3a", "Huge Mess"), "3a")
            .Select(s => s.Name).ToList();

        Assert.Equal(["Huge Mess", "Huge Mess Heart"], names);
    }

    // "Start" exists in nearly every chapter: the chapter is part of the address
    [Fact]
    public void FindAddressesASegmentByChapterAndName() {
        SheetBlock block = Fixtures.Parsed.CheckpointBlock;

        Assert.Equal("2a", block.Find("2a", "Start")?.Chapter);
        Assert.Null(block.Find("2a", "Hollows"));
    }

    private static SheetSegment Imported(string chapter, string name) =>
        Fixtures.Imported.Single(s => s.Chapter == chapter && s.Name == name);
}
