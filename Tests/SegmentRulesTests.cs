using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// one rule per imported row, and nothing that two rules could both claim
public class SegmentRulesTests {
    private static SegmentRule Rule(string chapter, string name) => TestRules.Find(chapter, name);

    // the rule a label alone derives, on a row of no table
    private static SegmentRule Synthetic(string label) =>
        SegmentRules.Build([new SheetRow(StandardsTab.ASides, "3a CP", label, "3a", "3a", label, "Huge Mess")])[0];

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
        "1c/1c", "2c/2c", "3c/3c", "4c/4c", "5c/5c", "6c/6c", "7c/7c", "8c/8c",
        "Farewell/Start",
        "Farewell/Start DTS",
    ];

    // a row given ChapterEnd by mistake would gain a frame without a sound
    [Fact]
    public void OnlyTheCSideRowsAreAWholeChapter() {
        Assert.Equal(["1c/1c", "2c/2c", "3c/3c", "4c/4c", "5c/5c", "6c/6c", "7c/7c", "8c/8c"],
            SegmentRules.All.Where(r => r.End == EndKind.ChapterEnd).Select(r => $"{r.Chapter}/{r.Name}"));
    }

    // every other row is NextRoom: a new Start-anchored row, or a new
    // wake-up, shows up here as a diff
    [Fact]
    public void CurrentRoomRowsAreTheChapterStartsAndTheWakeUps() {
        HashSet<string> currentRoom = SegmentRules.All.Where(r => r.Setup == StartSetup.CurrentRoom)
            .Select(r => $"{r.Chapter}/{r.Name}").ToHashSet();

        Assert.Equal(ExpectedCurrentRoomRows, currentRoom);
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

    // the sheet's spacing around a marker is irregular, and a row marking both
    // ends on the later of the two. Collectibles as an int: the enum is internal
    [Theory]
    [InlineData("Depths 📼 RTM", (int)Collectibles.Cassette)]
    [InlineData("📼RTM", (int)Collectibles.Cassette)]
    [InlineData("Shrine 💙 RTM", (int)Collectibles.Heart)]
    [InlineData("💙+📼 RTM", (int)(Collectibles.Heart | Collectibles.Cassette))]
    public void AnRtmLabelEndsAtWhatItMarks(string label, int endsOn) {
        SegmentRule rule = Synthetic(label);

        Assert.Equal((EndKind.Collect, (Collectibles)endsOn), (rule.End, rule.EndsOn));
    }

    [Theory]
    [InlineData("2a Start 💙 RC")]
    [InlineData("2a Start 💙 RC ")]
    public void AnRcLabelEndsAtTheRestartAndRequiresWhatItMarks(string label) {
        SegmentRule rule = Synthetic(label);

        Assert.Equal((EndKind.Restart, Collectibles.None, Collectibles.Heart), (rule.End, rule.EndsOn, rule.Requires));
    }

    // "Clear" on a checkpoint row means collect and keep going, not the
    // chapter's end, and an RTM marking nothing a run collects has nothing to
    // end on
    [Theory]
    [InlineData("Granny")]
    [InlineData("Start DTS")]
    [InlineData("Crossing 💙")]
    [InlineData("Shrine 💙 Clear")]
    [InlineData("Hollows 📼Clear")]
    [InlineData("1b Clear")]
    [InlineData("Plain RTM")]
    public void EveryOtherLabelEndsWithItsSegment(string label) {
        Assert.Equal(EndKind.NextStart, Synthetic(label).End);
    }

    // the markers are what a run must collect, wherever they sit in the label
    [Theory]
    [InlineData("Crossing 💙", (int)Collectibles.Heart)]
    [InlineData("Hollows 📼Clear", (int)Collectibles.Cassette)]
    [InlineData("7a Start 💎", (int)Collectibles.Gem)]
    [InlineData("Crossing", (int)Collectibles.None)]
    public void ALabelRequiresWhatItMarks(string label, int requires) {
        Assert.Equal((Collectibles)requires, Synthetic(label).Requires);
    }

    // "X DTS" keeps both dashes and the "X" beside it lost one; a Farewell row
    // without a twin, and any other tab's, has no dash rule
    [Fact]
    public void ADtsTwinSplitsItsRowOnTheDashes() {
        List<SegmentRule> rules = SegmentRules.Build([
            new SheetRow(StandardsTab.Farewell, "Farewell", "Singular", "Farewell", "Farewell", "Singular", "Singular"),
            new SheetRow(StandardsTab.Farewell, "Farewell", "Singular DTS", "Farewell", "Farewell", "Singular DTS", "Singular"),
            new SheetRow(StandardsTab.Farewell, "Farewell", "Stubbornness", "Farewell", "Farewell", "Stubbornness", "Stubbornness"),
            new SheetRow(StandardsTab.ASides, "1a CP", "Crossing DTS", "1a", "1a", "Crossing DTS", "Crossing"),
        ]);

        Assert.Equal(new int?[] { 1, 2, null, null }, rules.Select(r => r.Dashes));
    }

    // a marker read wrong mistimes its row without a sound
    [Fact]
    public void OnlyTheRcAndTapeRowsEndEarly() {
        Assert.Equal(["2a/Start Heart RC", "5a/b/Depths Tape", "6a/b/Hollows Tape"],
            SegmentRules.All.Where(r => r.End is EndKind.Collect or EndKind.Restart).Select(r => $"{r.Chapter}/{r.Name}"));
    }

    [Fact]
    public void RtmRowsEndAtTheCollect() {
        Assert.Equal((EndKind.Collect, Collectibles.Cassette),
            (Rule("6a/b", "Hollows Tape").End, Rule("6a/b", "Hollows Tape").EndsOn));
        Assert.Equal((EndKind.Collect, Collectibles.Cassette),
            (Rule("5a/b", "Depths Tape").End, Rule("5a/b", "Depths Tape").EndsOn));
        Assert.Equal(EndKind.NextStart, Rule("6a/b", "Hollows").End);
    }

    // timed to the restart, as Speed Run Tool's timer kept across it is; the
    // heart is what tells it from plain 2a Start
    [Fact]
    public void TheRcRowEndsAtTheRestartWithItsHeart() {
        SegmentRule rc = Rule("2a", "Start Heart RC");
        Assert.Equal((EndKind.Restart, Collectibles.None, Collectibles.Heart), (rc.End, rc.EndsOn, rc.Requires));
        Assert.Equal(EndKind.NextStart, Rule("2a", "Start").End);
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
