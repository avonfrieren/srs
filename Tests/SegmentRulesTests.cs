using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// one rule per imported row, and nothing that two rules could both claim
public class SegmentRulesTests {
    private static SegmentRule Rule(string chapter, string name) =>
        SegmentRules.Find(chapter, name) ?? throw new Exception($"no rule for {chapter}/{name}");

    private static readonly HashSet<string> ExpectedCurrentRoomRows = [
        "Prologue/Granny",
        "1a/Start",
        "2a/Start",
        "2a/Start Heart RC",
        "2a/Awake",
        "3a/Start",
        "4a/Start",
        "5a/b/5a Start",
        "5a/b/Unravelling",
        "5a/b/5b Start",
        "5a/b/Through the Mirror",
        "6a/b/6a Start",
        "6a/b/6b Start",
        "7a/7a Start",
        "8a/Start",
        "Farewell/Start",
        "Farewell/Start DTS",
    ];

    [Fact]
    public void CurrentRoomRowsAreTheChapterStartsAndTheWakeUps() {
        foreach (SegmentRule rule in SegmentRules.All) {
            bool expected = rule.Anchor == "Start"
                            || SegmentAutoDetect.CurrentRoomStarts.Contains((rule.Scope, rule.Anchor));
            Assert.True(expected == (rule.Setup == StartSetup.CurrentRoom), $"{rule.Chapter}/{rule.Name}");
        }

        Assert.Equal(StartSetup.CurrentRoom, Rule("Prologue", "Granny").Setup);
        // spec §12.2: a new Start-anchored row shows up here as a diff
        HashSet<string> currentRoom = SegmentRules.All.Where(r => r.Setup == StartSetup.CurrentRoom)
            .Select(r => $"{r.Chapter}/{r.Name}").ToHashSet();
        Assert.Equal(ExpectedCurrentRoomRows, currentRoom);
        Assert.Equal(StartSetup.CurrentRoom, Rule("2a", "Awake").Setup);
        Assert.Equal(StartSetup.CurrentRoom, Rule("5a/b", "Unravelling").Setup);
        Assert.Equal(StartSetup.CurrentRoom, Rule("5a/b", "Through the Mirror").Setup);
        Assert.Equal(StartSetup.CurrentRoom, Rule("7a", "7a Start").Setup);
        Assert.Equal(StartSetup.NextRoom, Rule("1a", "Crossing").Setup);
        Assert.Equal(StartSetup.NextRoom, Rule("2a", "Intervention").Setup);
        Assert.Equal(StartSetup.NextRoom, Rule("6a/b", "Lake").Setup);
        Assert.Equal(StartSetup.NextRoom, Rule("7a", "500m").Setup);
    }

    [Fact]
    public void OneRulePerImportedRow() {
        Assert.Equal(SheetRows.All.Length, SegmentRules.All.Count);
        Assert.Equal(SegmentRules.All.Count,
            SegmentRules.All.Select(r => (r.Chapter, r.Name)).Distinct().Count());
    }

    // two rules with the same start, end, requirements and exclusivity could not
    // be told apart by anything a run does
    [Fact]
    public void NoTwoRulesAreIndistinguishable() {
        var clashes = SegmentRules.All
            .GroupBy(r => (r.Scope, r.Anchor, r.Start, r.End, r.EndsOn, r.Requires, r.RequiresBerries, r.Dashes))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" / ", group.Select(r => r.Name)))
            .ToList();

        Assert.True(clashes.Count == 0, string.Join("\n", clashes));
    }

    [Fact]
    public void ACollectRuleEndsOnSomething() {
        Assert.All(SegmentRules.All.Where(r => r.End == EndKind.Collect),
            r => Assert.NotEqual(Collectibles.None, r.EndsOn));
    }

    // RoomMap.BerriesOf answers nothing for now, which is only safe while this holds
    [Fact]
    public void NoImportedRowRequiresBerriesYet() {
        Assert.DoesNotContain(SegmentRules.All, r => r.RequiresBerries);
    }

    [Fact]
    public void TheVariantsAreToldApartByWhatTheyCollect() {
        Assert.Equal(Collectibles.None, Rule("3a", "Huge Mess").Requires);
        Assert.Equal(Collectibles.Heart, Rule("3a", "Huge Mess Heart").Requires);
        Assert.Equal(EndKind.NextStart, Rule("3a", "Huge Mess Heart").End);
        Assert.Equal(EndKind.NextStart, Rule("4a", "Shrine Heart").End);
        Assert.Equal(Collectibles.Heart, Rule("4a", "Shrine Heart").Requires);
    }

    [Fact]
    public void RtmAndRcRowsEndAtTheCollect() {
        Assert.Equal((EndKind.Collect, Collectibles.Cassette),
            (Rule("6a/b", "Hollows Tape").End, Rule("6a/b", "Hollows Tape").EndsOn));
        Assert.Equal((EndKind.Collect, Collectibles.Cassette),
            (Rule("5a/b", "Depths Tape").End, Rule("5a/b", "Depths Tape").EndsOn));
        Assert.Equal((EndKind.Collect, Collectibles.Heart),
            (Rule("2a", "Start Heart RC").End, Rule("2a", "Start Heart RC").EndsOn));
        Assert.Equal(EndKind.NextStart, Rule("6a/b", "Hollows").End);
    }

    [Fact]
    public void FarewellsDtsTwinsAreExclusive() {
        Assert.Equal(2, Rule("Farewell", "Start DTS").Dashes);
        Assert.Equal(1, Rule("Farewell", "Start").Dashes);
        Assert.Equal(2, Rule("Farewell", "Determination DTS").Dashes);
        Assert.Equal(1, Rule("Farewell", "Determination").Dashes);
        // the skip is over by Stubbornness: no twin, no dash requirement
        Assert.Null(Rule("Farewell", "Stubbornness").Dashes);
        Assert.Null(Rule("1a", "Start").Dashes);
    }

    [Fact]
    public void HeadsAndTheTailAreTheSheetsConstants() {
        Assert.Equal(TimeSpan.FromMilliseconds(5508).Ticks, Rule("7a", "7a Start").HeadTicks);
        Assert.Equal(StartKind.AfterLaunch, Rule("7a", "7a Start").Start);
        Assert.Equal(TimeSpan.FromMilliseconds(1037).Ticks, Rule("Prologue", "Granny").HeadTicks);
        Assert.Equal(TimeSpan.FromMilliseconds(561).Ticks, Rule("Prologue", "Granny").TailTicks);
        Assert.Equal(0, Rule("7a", "500m").HeadTicks);
        Assert.Equal(StartKind.Room, Rule("7a", "500m").Start);
        Assert.Equal(StartKind.Room, Rule("2a", "Awake").Start);
    }

    [Fact]
    public void RulesKeepSheetOrder() {
        Assert.Equal(Enumerable.Range(0, SegmentRules.All.Count), SegmentRules.All.Select(r => r.Order));
    }
}
