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
        SegmentRules.Build([new SheetRow(StandardsTab.ASides, "3a CP", label, "3a", label, "Huge Mess")])[0];

    private static readonly HashSet<string> ExpectedCurrentRoomRows = [
        "Prologue/Granny",
        "1a/Start",
        "1a/IL", "1a/IL Tape RTM", "1a/IL Tape Clear", "1a/IL Heart Tape RTM",
        "2a/Start",
        "2a/Start Heart RC", "2a/Start Tape Clear", "2a/Start Tape RTM",
        "2a/Awake",
        "2a/IL", "2a/IL Tape Clear",
        "3a/Start",
        "3a/IL", "3a/IL Heart Clear", "3a/IL Tape RTM", "3a/IL Heart Tape Clear", "3a/IL Heart Tape RTM",
        "4a/Start", "4a/Start Tape Clear", "4a/Start Tape RTM",
        "4a/IL", "4a/IL Heart Clear", "4a/IL Tape Clear", "4a/IL Heart Tape Clear", "4a/IL Heart Tape RTM",
        "5a/Start",
        "5a/Unravelling",
        "5a/IL", "5a/IL Tape RTM", "5a/IL Heart Tape RTM",
        "1b/Start", "1b/IL", "2b/Start", "2b/IL", "3b/Start", "3b/IL", "4b/Start", "4b/IL",
        "5b/Start",
        "5b/Through the Mirror",
        "5b/IL",
        "6a/Start",
        "6a/IL", "6a/IL Tape Clear", "6a/IL Tape RTM", "6a/IL Heart Tape RTM",
        "6b/Start", "6b/IL",
        "7b/Start", "7b/IL", "8b/Start", "8b/IL",
        "7a/Start Gem",
        "2a/FC Start", "4a/FC Start", "7a/FC Start",
        "2a/FC IL", "3a/FC IL", "4a/FC IL", "7a/FC IL", "8a/FC IL", "Farewell/FC DTS IL", "Farewell/FC No DTS IL",
        "7a/Start",
        "7a/IL", "7a/IL Tape Clear", "7a/IL Tape RTM", "7a/IL Heart Tape RTM",
        "8a/Start",
        "8a/IL", "8a/IL Tape Clear",
        "1c/1c", "2c/2c", "3c/3c", "4c/4c", "5c/5c", "6c/6c", "7c/7c", "8c/8c",
        "Farewell/Start",
        "Farewell/Start DTS",
        "Farewell/DTS IL",
        "Farewell/No DTS IL",
        "1a/ARB Start to Heart", "1a/ARB Start",
        "2a/ARB Start", "2a/ARB Awake", "2a/ARB IL",
        "3a/ARB Start", "3a/ARB IL",
        "4a/ARB Start", "4a/ARB IL",
        "5a/ARB Unravelling",
        "7a/ARB Start", "7a/ARB IL",
        "8a/ARB IL",
    ];

    // a row given ChapterEnd by mistake would stay open across its checkpoints
    [Fact]
    public void OnlyTheIlAndCSideRowsAreAWholeChapter() {
        Assert.Equal([
                "1a/IL", "1a/IL Tape Clear", "2a/IL", "2a/IL Tape Clear",
                "3a/IL", "3a/IL Heart Clear", "3a/IL Heart Tape Clear",
                "4a/IL", "4a/IL Heart Clear", "4a/IL Tape Clear", "4a/IL Heart Tape Clear",
                "5a/IL", "6a/IL", "6a/IL Tape Clear", "7a/IL", "7a/IL Tape Clear",
                "8a/IL", "8a/IL Tape Clear",
                "1b/IL", "2b/IL", "3b/IL", "4b/IL", "5b/IL", "6b/IL", "7b/IL", "8b/IL",
                "1c/1c", "2c/2c", "3c/3c", "4c/4c", "5c/5c", "6c/6c", "7c/7c", "8c/8c",
                "Farewell/DTS IL", "Farewell/No DTS IL",
                "2a/ARB IL", "3a/ARB IL", "4a/ARB IL", "7a/ARB IL", "8a/ARB IL",
                "2a/FC IL", "3a/FC IL", "4a/FC IL", "7a/FC IL", "8a/FC IL", "Farewell/FC DTS IL", "Farewell/FC No DTS IL",
            ],
            SegmentRules.All.Where(r => r.End == EndKind.ChapterEnd).Select(r => $"{r.Scope}/{r.Name}"));
    }

    // an IL starts where its chapter's Start does: 7A's after the launch, with the same head
    [Fact]
    public void AnIlStartsLikeItsChaptersStart() {
        foreach (SegmentRule il in SegmentRules.All.Where(r => r.Name.Contains("IL"))) {
            SegmentRule start = SegmentRules.All.Single(r =>
                r.Scope == il.Scope && r.Name == "Start");

            Assert.Equal((start.Anchor, start.Start, start.Setup, start.HeadTicks, start.TailTicks),
                (il.Anchor, il.Start, il.Setup, il.HeadTicks, il.TailTicks));
        }
    }

    // every other row is NextRoom or MapSpawn (TheMapSpawnRows): a new Start-anchored row, or a new
    // wake-up, shows up here as a diff
    [Fact]
    public void CurrentRoomRowsAreTheChapterStartsAndTheWakeUps() {
        HashSet<string> currentRoom = SegmentRules.All.Where(r => r.Setup == StartSetup.CurrentRoom)
            .Select(r => $"{r.Scope}/{r.Name}").ToHashSet();

        Assert.Equal(ExpectedCurrentRoomRows, currentRoom);
    }

    [Fact]
    public void OneRulePerImportedRow() {
        Assert.Equal(SheetRows.All.Length, SegmentRules.All.Count);
        Assert.Equal(SegmentRules.All.Count,
            SegmentRules.All.Select(r => (r.Scope, r.Name)).Distinct().Count());
    }

    // two rules with the same start, setup, end, requirements and exclusivity could not
    // be told apart by anything a run does
    [Fact]
    public void NoTwoRulesAreIndistinguishable() {
        var clashes = SegmentRules.All
            .GroupBy(r => (r.Scope, r.Anchor, r.Start, r.Setup, r.End, r.EndsOn, r.Requires, r.RequiresBerries, r.Dashes))
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

    // a berry row with no set would never be met, and a set on another row would never be read
    [Fact]
    public void OnlyTheBerryTabsRowsRequireBerries() {
        Assert.All(SegmentRules.All, r => {
            // 6A has no berry, and 8A's row is typed on the A-side tab under an A-side name
            bool berryTab = r.Name.StartsWith("ARB ") || (r.Name.StartsWith("FC ") && r.Scope != "6a");
            Assert.Equal(berryTab, r.RequiresBerries);
        });
        Assert.Equal(39, SegmentRules.All.Count(r => r.Name.StartsWith("ARB ")));
        Assert.Equal(20, SegmentRules.All.Count(r => r.Name.StartsWith("FC ") && r.RequiresBerries));
        Assert.Equal(12, SegmentRules.All.Count(r => r.RequiresBerries && r.ChapterRun));
    }

    [Fact]
    public void TheMapSpawnRows() {
        Assert.Equal(
            ["1a/ARB Crossing to Heart", "1a/ARB Crossing", "1a/ARB Chasm", "4a/ARB Cliff Face (from RTM)", "5a/ARB Depths",
             "5a/FC Depths", "6a/FC Hollows (from RTM)"],
            SegmentRules.All.Where(r => r.Setup == StartSetup.MapSpawn).Select(r => $"{r.Scope}/{r.Name}"));
    }

    // 7A's heart needs the gems, which the berry route does not take
    [Fact]
    public void TheBerryRowsThatNeedTheHeart() {
        Assert.Equal(
            ["1a/ARB Crossing to Heart", "1a/ARB Start to Heart", "1a/ARB Crossing", "3a/ARB Huge Mess", "4a/ARB Shrine",
             "3a/ARB IL", "4a/ARB IL"],
            SegmentRules.All.Where(r => r.Name.StartsWith("ARB ") && r.Requires == Collectibles.Heart)
                .Select(r => $"{r.Scope}/{r.Name}"));
        Assert.All(SegmentRules.All.Where(r => r.Name.StartsWith("ARB ")),
            r => Assert.True(r.Requires is Collectibles.None or Collectibles.Heart));
    }

    // the berry rows on the virtual split and on the override start and end
    // where their A-side rows do, since both hang on the anchor
    [Fact]
    public void ABerryRowKeepsItsAnchorsBoundaries() {
        Assert.Equal("Heart of the Mountain", Rule("8a", "ARB HotM Vertical").Anchor);
        Assert.Equal("Awake", Rule("2a", "ARB Awake").Anchor);
        Assert.Equal("2500 M", Rule("7a", "ARB 2500m-full").Anchor);
        Assert.Equal((Rule("7a", "Start").Start, Rule("7a", "Start").HeadTicks),
            (Rule("7a", "ARB Start").Start, Rule("7a", "ARB Start").HeadTicks));
    }

    // 3000m's three pieces chain as checkpoints do, the first from where
    // 3000m starts, and a berry piece asks for its own rooms' berries only
    [Fact]
    public void TheDraftRowsCutThe3000mCheckpoint() {
        Assert.All(new[] { "Downdraft", "Updraft", "Nodraft" }, name => {
            SegmentRule plain = Rule("7a", name);
            SegmentRule berry = Rule("7a", "ARB " + name);
            Assert.Equal((name, StartSetup.NextRoom, EndKind.NextStart, false), (plain.Anchor, plain.Setup, plain.End, plain.ChapterRun));
            Assert.Equal((plain.Anchor, plain.Setup, plain.End, Collectibles.None), (berry.Anchor, berry.Setup, berry.End, berry.Requires));
            Assert.Null(plain.Berries);
            // a virtual anchor is no checkpoint of the map: the set names rooms
            Assert.Null(berry.Berries.Checkpoint);
            Assert.Single(berry.Berries.Rooms);
        });
        Assert.Equal(SegmentAutoDetect.EntryRooms[("7a", "3000 M")], SegmentAutoDetect.EntryRooms[("7a", "Downdraft")]);
    }

    // the rooms of the three pieces, by value: RunTrackerTests builds its own copy
    [Theory]
    [InlineData("Downdraft", "g-00", "f-11", "Updraft")]
    [InlineData("Updraft", "g-01", "g-00b", "Nodraft")]
    [InlineData("Nodraft", "g-02", "g-01", null)]
    public void TheDraftRowsRoomsAreTheseOnes(string name, string start, string entry, string splitInto) {
        Assert.Equal(start, SegmentAutoDetect.StartRoomOverrides[("7a", name)]);
        Assert.Equal(entry, SegmentAutoDetect.EntryRooms[("7a", name)]);
        Assert.Equal(splitInto, SegmentAutoDetect.SplitCheckpoints.GetValueOrDefault(("7a", name)));
    }

    // a Full Clear label says nothing of what the row collects: the whole table, as the owner gave it
    [Theory]
    [InlineData("2a", "FC Start", (int)(Collectibles.Cassette), 0)]
    [InlineData("2a", "FC IL", (int)(Collectibles.Cassette), 0)]
    [InlineData("3a", "FC Elevator Shaft", (int)(Collectibles.Cassette), 0)]
    [InlineData("3a", "FC IL", (int)(Collectibles.Heart | Collectibles.Cassette), 0)]
    [InlineData("4a", "FC Start", (int)(Collectibles.Cassette), 0)]
    [InlineData("4a", "FC IL", (int)(Collectibles.Heart | Collectibles.Cassette), 0)]
    [InlineData("5a", "FC Depths", (int)(Collectibles.Heart | Collectibles.Cassette), 0)]
    [InlineData("6a", "FC Hollows (from RTM)", (int)(Collectibles.None), 0)]
    [InlineData("7a", "FC Start", (int)(Collectibles.Gem), 1)]
    [InlineData("7a", "FC 500m", (int)(Collectibles.Gem), 1)]
    [InlineData("7a", "FC 1000m", (int)(Collectibles.Gem), 1)]
    [InlineData("7a", "FC 1500m", (int)(Collectibles.Gem | Collectibles.Cassette), 1)]
    [InlineData("7a", "FC 2000m", (int)(Collectibles.Gem), 1)]
    [InlineData("7a", "FC 2500m", (int)(Collectibles.Gem), 1)]
    [InlineData("7a", "FC 3000m", (int)(Collectibles.Heart), 0)]
    [InlineData("7a", "FC Downdraft", (int)(Collectibles.Heart), 0)]
    [InlineData("7a", "FC IL", (int)(Collectibles.Gem | Collectibles.Cassette | Collectibles.Heart), 6)]
    [InlineData("8a", "HotM Horizontal Tape", (int)(Collectibles.Cassette), 0)]
    [InlineData("8a", "FC IL", (int)(Collectibles.Cassette), 0)]
    [InlineData("Farewell", "FC Moon Berry", (int)(Collectibles.None), 0)]
    [InlineData("Farewell", "FC DTS IL", (int)(Collectibles.None), 0)]
    [InlineData("Farewell", "FC No DTS IL", (int)(Collectibles.None), 0)]
    public void WhatAFullClearRowCollects(string chapter, string name, int requires, int gems) {
        SegmentRule rule = Rule(chapter, name);

        Assert.Equal(((Collectibles)requires, gems, Collectibles.None), (rule.Requires, rule.Gems, rule.EndsOn));
        Assert.Equal(name.EndsWith("IL") ? EndKind.ChapterEnd : EndKind.NextStart, rule.End);
    }

    [Fact]
    public void TheFullClearRowsOffTheDefaultBerrySet() {
        Assert.Null(Rule("6a", "FC Hollows (from RTM)").Berries);
        Assert.Null(Rule("8a", "HotM Horizontal Tape").Berries);
        Assert.Equal(["g-00b"], Rule("7a", "FC Downdraft").Berries.Rooms);
        Assert.Equal(["g-00b"], Rule("7a", "ARB Downdraft").Berries.Rooms);
        Assert.Equal(["g-01"], Rule("7a", "ARB Updraft").Berries.Rooms);
        Assert.Equal(["g-03"], Rule("7a", "ARB Nodraft").Berries.Rooms);
        Assert.All(new[] { "FC Moon Berry", "FC DTS IL", "FC No DTS IL" },
            name => Assert.Same(BerrySet.MoonBerry, Rule("Farewell", name).Berries));
        Assert.Equal("Start", Rule("2a", "FC Start").Berries.Checkpoint);
        Assert.Same(BerrySet.WholeChapter, Rule("7a", "FC IL").Berries);
    }

    // the Farewell tab's own ILs get their dash count from their label; these two cannot
    [Fact]
    public void TheFarewellFullClearRunsKeepTheDashRule() {
        Assert.Equal((2, 1, (int?)null),
            (Rule("Farewell", "FC DTS IL").Dashes.Value, Rule("Farewell", "FC No DTS IL").Dashes.Value,
             Rule("Farewell", "FC Moon Berry").Dashes));
    }

    // Contains compares the berry flag only: a Full Clear row must ask for its berry twin's very
    // set, or it would pass for more specific than a row whose berries it does not hold
    [Fact]
    public void AFullClearRowAsksForItsBerryTwinsBerries() {
        foreach (SegmentRule fc in SegmentRules.All.Where(r => r.Name.StartsWith("FC ") && r.Scope != "Farewell")) {
            string twin = "ARB " + fc.Name[3..].Replace("2500m", "2500m-full");
            if (SegmentRules.All.SingleOrDefault(r => r.Scope == fc.Scope && r.Name == twin) is { } arb) {
                Assert.Equal((arb.Berries, arb.Anchor, arb.Setup, arb.End == EndKind.Collect ? fc.End : arb.End),
                    (fc.Berries, fc.Anchor, fc.Setup, fc.End));
            } else {
                Assert.Contains($"{fc.Scope}/{fc.Name}", new[] { "6a/FC Hollows (from RTM)" });
            }
        }
    }

    // what the timer row shows when a 7A checkpoint closes, by what the run collected
    [Fact]
    public void TheMostSpecificRowMetIsShown() {
        SegmentRule plain = Rule("7a", "500m"), gem = Rule("7a", "500m Gem"), arb = Rule("7a", "ARB 500m"),
            fc = Rule("7a", "FC 500m");

        Assert.Same(fc, Shown(plain, gem, arb, fc));
        Assert.Same(gem, Shown(plain, gem));
        Assert.Same(arb, Shown(plain, arb));
        Assert.Same(Rule("7a", "FC IL"),
            ShownOf(true, [Rule("7a", "IL"), Rule("7a", "IL Tape Clear"), Rule("7a", "ARB IL"), Rule("7a", "FC IL")]));

        static SegmentRule Shown(params SegmentRule[] met) => ShownOf(false, met);
    }

    private static SegmentRule ShownOf(bool chapterRun, SegmentRule[] met) =>
        met[Specificity.MostSpecific(met, chapterRun)];

    // a gem row is its plain row plus the gem its label marks
    [Fact]
    public void AGemRowAsksForItsCheckpointsGem() {
        Assert.All(new[] { "Start", "500m", "1000m", "2000m", "2500m" }, name => {
            SegmentRule plain = Rule("7a", name);
            SegmentRule gem = Rule("7a", name + " Gem");
            Assert.Equal((plain.Anchor, plain.Start, plain.Setup, plain.End, plain.HeadTicks),
                (gem.Anchor, gem.Start, gem.Setup, gem.End, gem.HeadTicks));
            Assert.Equal((Collectibles.Gem, 1), (gem.Requires, gem.Gems));
        });
        Assert.Equal((Collectibles.Gem | Collectibles.Cassette, 1, EndKind.NextStart),
            (Rule("7a", "1500m Gem Tape").Requires, Rule("7a", "1500m Gem Tape").Gems, Rule("7a", "1500m Gem Tape").End));
        Assert.All(SegmentRules.All, r => Assert.Equal(r.Gems > 0, (r.Requires & Collectibles.Gem) != 0));
    }

    // what the rows above the timer and the export screen print
    [Fact]
    public void NoTwoRowsShowTheSameName() {
        Assert.Equal(SegmentRules.All.Count,
            SegmentRules.All.Select(r => TierLine.NameOf(r.Scope, r.Name)).Distinct().Count());
    }

    [Fact]
    public void TheVariantsAreToldApartByWhatTheyCollect() {
        Assert.Equal(Collectibles.None, Rule("3a", "Huge Mess").Requires);
        Assert.Equal(Collectibles.Heart, Rule("3a", "Huge Mess Heart").Requires);
        Assert.Equal(EndKind.NextStart, Rule("3a", "Huge Mess Heart").End);
        Assert.Equal(EndKind.NextStart, Rule("4a", "Shrine Heart Clear").End);
        Assert.Equal(Collectibles.Heart, Rule("4a", "Shrine Heart Clear").Requires);
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

    // "Clear" on an A-side checkpoint row means collect and keep going, not the
    // chapter's end, and an RTM marking nothing a run collects has nothing to
    // end on
    [Theory]
    [InlineData("Granny")]
    [InlineData("Start DTS")]
    [InlineData("Crossing 💙")]
    [InlineData("Shrine 💙 Clear")]
    [InlineData("Hollows 📼Clear")]
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
            new SheetRow(StandardsTab.Farewell, "Farewell", "Singular", "Farewell", "Singular", "Singular"),
            new SheetRow(StandardsTab.Farewell, "Farewell", "Singular DTS", "Farewell", "Singular DTS", "Singular"),
            new SheetRow(StandardsTab.Farewell, "Farewell", "Stubbornness", "Farewell", "Stubbornness", "Stubbornness"),
            new SheetRow(StandardsTab.ASides, "1a CP", "Crossing DTS", "1a", "Crossing DTS", "Crossing"),
        ]);

        Assert.Equal(new int?[] { 1, 2, null, null }, rules.Select(r => r.Dashes));
    }

    // a marker read wrong mistimes its row without a sound
    [Fact]
    public void OnlyTheRcRtmAndToHeartRowsEndEarly() {
        Assert.Equal(
            SheetRows.All.Where(r => r.Label.EndsWith("RTM") || r.Label.EndsWith("RC") || r.Label.EndsWith("to Heart"))
                .Select(r => $"{r.Scope}/{r.Name}"),
            SegmentRules.All.Where(r => r.End is EndKind.Collect or EndKind.Restart).Select(r => $"{r.Scope}/{r.Name}"));
    }

    // an IL's "RTM" variant stops at the later collect, not at the chapter's end
    [Fact]
    public void AnIlsRtmVariantKeepsItsCollect() {
        Collectibles both = Collectibles.Heart | Collectibles.Cassette;
        Assert.Equal((EndKind.Collect, both, both),
            (Rule("3a", "IL Heart Tape RTM").End, Rule("3a", "IL Heart Tape RTM").EndsOn,
                Rule("3a", "IL Heart Tape RTM").Requires));
    }

    // what the rows above the timer split on: an IL "RTM" is a chapter run
    // too, and no checkpoint row is
    [Fact]
    public void TheIlAndCSideRowsAreChapterRuns() {
        Assert.All(SegmentRules.All, r => Assert.Equal(r.Name.Contains("IL") || r.Scope.EndsWith('c'), r.ChapterRun));
    }

    // the B-sides tab has no IL block: a chapter's "Clear" is the whole chapter
    // there, and only there
    [Fact]
    public void ABSidesClearIsTheWholeChapter() {
        SheetRow clear = new(StandardsTab.BSides, "1b", "1b Clear", "1b", "IL", "Start");

        Assert.Equal((EndKind.ChapterEnd, true), (SegmentRules.Build([clear])[0].End, SegmentRules.Build([clear])[0].ChapterRun));
        Assert.Equal((EndKind.NextStart, false), (Synthetic("1b Clear").End, Synthetic("1b Clear").ChapterRun));
    }

    [Fact]
    public void RtmRowsEndAtTheCollect() {
        Assert.Equal((EndKind.Collect, Collectibles.Cassette),
            (Rule("6a", "Hollows Tape RTM").End, Rule("6a", "Hollows Tape RTM").EndsOn));
        Assert.Equal((EndKind.Collect, Collectibles.Cassette),
            (Rule("5a", "Depths Tape RTM").End, Rule("5a", "Depths Tape RTM").EndsOn));
        Assert.Equal(EndKind.NextStart, Rule("6a", "Hollows").End);
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
        Assert.Equal(2, Rule("Farewell", "DTS IL").Dashes);
        Assert.Equal(1, Rule("Farewell", "No DTS IL").Dashes);
        Assert.Equal(1, Rule("Farewell", "Determination").Dashes);
        // the skip is over by Stubbornness: no twin, no dash requirement
        Assert.Null(Rule("Farewell", "Stubbornness").Dashes);
        Assert.Null(Rule("1a", "Start").Dashes);
    }

    [Fact]
    public void HeadsAndTheTailAreTheSheetsConstants() {
        Assert.Equal(TimeSpan.FromMilliseconds(5508).Ticks, Rule("7a", "Start").HeadTicks);
        Assert.Equal(StartKind.AfterLaunch, Rule("7a", "Start").Start);
        Assert.Equal(TimeSpan.FromMilliseconds(1037).Ticks, Rule("Prologue", "Granny").HeadTicks);
        Assert.Equal(TimeSpan.FromMilliseconds(544).Ticks, Rule("Prologue", "Granny").TailTicks);
        Assert.Equal(Rule("7a", "Start").HeadTicks, Rule("7b", "Start").HeadTicks);
        Assert.Equal(StartKind.AfterLaunch, Rule("7b", "Start").Start);
        Assert.Equal(0, Rule("7a", "500m").HeadTicks);
        Assert.Equal(StartKind.Room, Rule("7a", "500m").Start);
        Assert.Equal(StartKind.Room, Rule("2a", "Awake").Start);
    }

    [Fact]
    public void RulesKeepSheetOrder() {
        Assert.Equal(Enumerable.Range(0, SegmentRules.All.Count), SegmentRules.All.Select(r => r.Order));
    }

    [Fact]
    public void ABerryTabRowRequiresItsCheckpointsBerries() {
        SegmentRule rule = Rule("2a", "ARB Intervention");

        Assert.True(rule.RequiresBerries);
        Assert.Equal("Intervention", rule.Berries.Checkpoint);
        Assert.Null(rule.Berries.Rooms);
        Assert.Null(rule.Berries.Except);
        Assert.False(rule.Berries.Chapter);
        Assert.Equal((StartSetup.NextRoom, EndKind.NextStart, Collectibles.None, false),
            (rule.Setup, rule.End, rule.Requires, rule.ChapterRun));
        Assert.Null(rule.EntryRoom);
    }

    // the second block names a row by its chapter cell
    [Fact]
    public void ABerryTabChapterRowIsAChapterRunOverEveryBerry() {
        SegmentRule rule = Rule("2a", "ARB IL");

        Assert.True(rule.ChapterRun);
        Assert.Equal((StartSetup.CurrentRoom, EndKind.ChapterEnd, Collectibles.None),
            (rule.Setup, rule.End, rule.Requires));
        Assert.True(rule.Berries.Chapter);
    }

    // no emoji in the label says so
    [Theory]
    [InlineData("ARB Start to Heart")]
    [InlineData("ARB Crossing to Heart")]
    public void TheToHeartRowsEndOnTheHeart(string name) {
        SegmentRule rule = Rule("1a", name);

        Assert.Equal((EndKind.Collect, Collectibles.Heart, Collectibles.Heart), (rule.End, rule.EndsOn, rule.Requires));
        Assert.False(rule.ChapterRun);
    }
}
