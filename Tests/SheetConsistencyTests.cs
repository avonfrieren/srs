using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// The row table and the room tables (start-room overrides, split checkpoints,
// untimed heads and tails) address checkpoints by name, and nothing forces
// them to agree with each other or with the sheet. A rename on either side
// degrades silently: a row stops being imported, or a segment stops starting
// where it should. These tests cross-check the tables against each other and
// against the sheet.
public class SheetConsistencyTests {
    // every raw (chapter, checkpoint) pair present in the exported tabs. The
    // Farewell tab is parsed under the same implicit chapter the importer
    // gives it, so its Import keys read like the other tabs'
    private static readonly HashSet<(string, string)> RawRows = [
        .. new[] { (Fixtures.ASides, (string)null), (Fixtures.BSides, null), (Fixtures.Farewell, "Farewell") }
            .SelectMany(tab => SheetData.ParseBlocks(tab.Item1, tab.Item2))
            .SelectMany(block => block.Segments)
            .Select(segment => (segment.Chapter, segment.Name))
    ];

    [Fact]
    public void EveryStartSetupKeyIsAnImportedAnchor() {
        HashSet<(string, string)> anchors = SheetRows.All.Select(r => (r.Scope, r.Anchor)).ToHashSet();
        Assert.All(SegmentAutoDetect.CurrentRoomStarts, key => Assert.Contains(key, anchors));
        Assert.All(SegmentAutoDetect.SpawnOffsets.Keys, key => Assert.Contains(key, anchors));
    }

    // a Current Room start that is not a chapter's "Start" opens on a restart
    // only from its WakeUpSpawns entry: a new one left out would never open
    [Fact]
    public void EveryWakeUpStartHasASpawn() {
        List<(string, string)> wakeUps = SegmentRules.All
            .Where(rule => rule.Setup == StartSetup.CurrentRoom && rule.Anchor != "Start")
            .Select(rule => (rule.Scope, rule.Anchor))
            .Distinct().Order().ToList();
        List<(string, string)> spawns = SegmentAutoDetect.WakeUpSpawns.Keys.Select(key => (key.Scope, key.GameName)).Order().ToList();
        Assert.Equal(wakeUps, spawns);
        Assert.Equal(SegmentAutoDetect.CurrentRoomStarts.Select(key => (key.Scope, key.GameName)).Order().ToList(), spawns);
    }

    // the allowlist still matches the sheet. This is the test that catches a
    // rename on the sheet's side: refresh Tests/Fixtures/*.csv, and any row the
    // mod expects that no longer exists shows up here by name
    [Fact]
    public void EveryImportedRowStillExistsInTheSheet() {
        List<(string, string)> missing = SheetRows.All
            .Select(row => (row.SheetChapter, row.Label))
            .Where(key => !RawRows.Contains(key))
            .ToList();

        Assert.Empty(missing);
    }

    // the same check, run by the mod itself on every download, where no
    // one has to refresh a fixture for it to fire
    [Fact]
    public void TheFixturesMissNoImportedRow() {
        Assert.Empty(Fixtures.Parsed.MissingRows);
    }

    // the 5A row that sat unimported for 20 days, spelled the way srs used to
    [Fact]
    public void ARowTheSheetRenamedIsReportedByItsImportKey() {
        string renamed = Fixtures.ASides.Replace(",Unravelling,", ",Unraveling,");

        SheetData data = SheetData.Parse(renamed, Fixtures.BSides, Fixtures.Farewell);

        Assert.Equal([("5a CP", "Unravelling")], data.MissingRows);
    }

    // every imported segment carries the end condition its raw sheet name
    // declares. Only a row that ends in RTM or RC stops at what it collects:
    // the two 📼 RTM rows at the cassette, "2a Start 💙 RC" at the heart. The
    // other two hearts are Clear rows — collect and keep going — so they end
    // at the next in-game checkpoint, like everything else (or at the
    // chapter's completion when there is none, resolved at runtime). A marker
    // slipping through Import unnoticed would silently mistime the segment
    [Fact]
    public void EveryImportedSegmentEndsTheWayItsRawNameDeclares() {
        static HashSet<(string, string)> EndingAt(EndCondition condition) => [
            .. Fixtures.Imported
                .Where(segment => segment.End == condition)
                .Select(segment => (segment.Chapter, segment.Name))
        ];

        Assert.Equal([("5a/b", "Depths Tape"), ("6a/b", "Hollows Tape")], EndingAt(EndCondition.Cassette));
        Assert.Equal([("2a", "Start Heart RC")], EndingAt(EndCondition.Heart));
        Assert.All(
            Fixtures.Imported.Where(segment =>
                segment.End != EndCondition.Cassette && segment.End != EndCondition.Heart),
            segment => Assert.Equal(EndCondition.Checkpoint, segment.End));
    }

    [Fact]
    public void EveryImportedSegmentIsAnchoredToAGameCheckpoint() {
        List<string> unanchored = Fixtures.Imported
            .Where(segment => !SheetRows.TryFind(segment.Chapter, segment.Name, out SheetRow row) || row.Anchor == null)
            .Select(segment => $"{segment.Chapter}/{segment.Name}")
            .ToList();

        Assert.Empty(unanchored);
    }

    // an override keyed on a checkpoint no row anchors would never fire, and the
    // previous segment would keep ending at the checkpoint's own room
    [Fact]
    public void EveryStartRoomOverrideTargetsAnAnchoredCheckpoint() {
        Assert.All(SegmentAutoDetect.StartRoomOverrides.Keys, key =>
            Assert.Contains(SheetRows.All, row => row.Scope == key.Scope && row.Anchor == key.GameName));
    }

    [Fact]
    public void EverySplitCheckpointHasBothHalvesAnchored() {
        foreach (KeyValuePair<(string Scope, string GameName), string> entry in SegmentAutoDetect.SplitCheckpoints) {
            Assert.Contains(SheetRows.All, row => row.Scope == entry.Key.Scope && row.Anchor == entry.Key.GameName);
            Assert.Contains(SheetRows.All, row => row.Scope == entry.Key.Scope && row.Anchor == entry.Value);
            Assert.Contains((entry.Key.Scope, entry.Value), SegmentAutoDetect.StartRoomOverrides.Keys);
        }

        Assert.Equal("HotM Horizontal", SegmentAutoDetect.SplitCheckpoints[("8a", "Heart of the Mountain")]);
        Assert.Equal("d-08", SegmentAutoDetect.StartRoomOverrides[("8a", "HotM Horizontal")]);
    }

    // heads, tails and launch starts are the sheet's constants, keyed on an
    // anchored checkpoint: an entry keyed on a checkpoint no row anchors would
    // never apply, and the segment would be compared against thresholds that
    // include a part of it, silently several tiers too high. The values are
    // pinned because the game cannot derive them
    [Fact]
    public void EveryUntimedHeadTargetsAKnownCheckpointAndKeepsItsValue() {
        bool Anchored((string Scope, string GameName) key) =>
            SheetRows.All.Any(row => row.Scope == key.Scope && row.Anchor == key.GameName);

        Assert.All(SegmentAutoDetect.UntimedSegmentHead.Keys, key => Assert.True(Anchored(key), key.ToString()));
        Assert.All(SegmentAutoDetect.UntimedSegmentTail.Keys, key => Assert.True(Anchored(key), key.ToString()));
        Assert.All(SegmentAutoDetect.AfterLaunchStarts, key => Assert.True(Anchored(key), key.ToString()));
        Assert.Equal(TimeSpan.FromMilliseconds(5508), SegmentAutoDetect.UntimedSegmentHead[("7a", "Start")]);
        Assert.Equal(TimeSpan.FromMilliseconds(1037), SegmentAutoDetect.UntimedSegmentHead[("Prologue", "Start")]);
        Assert.Equal(TimeSpan.FromMilliseconds(561), SegmentAutoDetect.UntimedSegmentTail[("Prologue", "Start")]);
    }

    // (chapter, name) is the address the row table and the exports use, so two
    // sheet rows must never collapse onto one
    [Fact]
    public void ImportedCheckpointsAreUniquelyAddressed() {
        List<(string Chapter, string Name)> duplicates = Fixtures.Imported
            .GroupBy(segment => (segment.Chapter, segment.Name))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    // canary for a restructured sheet: not a full snapshot of the data, just
    // the shape of what gets imported
    [Fact]
    public void ImportsTheExpectedCheckpointsInRouteOrder() {
        Assert.Equal(SheetRows.All.Length, Fixtures.Parsed.SegmentCount);
        Assert.Equal(
            ["Prologue", "1a", "2a", "3a", "4a", "5a/b", "6a/b", "7a", "8a", "Farewell"],
            Fixtures.Parsed.CheckpointBlock.Chapters());
    }

    // the two cassette routes the owner asked for in v2.0.0; they are the only
    // emoji rows kept. They start at the same in-game checkpoint as their
    // non-cassette sibling, and their runs end at the cassette collect, not in
    // any room
    [Theory]
    [InlineData("5a/b", "Depths Tape")]
    [InlineData("6a/b", "Hollows Tape")]
    public void ImportsTheCassetteCheckpointsEndingAtTheCollect(string chapter, string name) {
        SheetSegment segment = Assert.Single(Fixtures.Imported,
            s => s.Chapter == chapter && s.Name == name);

        Assert.Equal(EndCondition.Cassette, segment.End);
    }

    // the tier columns are read positionally from the header row, so their
    // names and order are part of the contract with TierComparison's palette
    [Fact]
    public void ReadsTheTierColumnsFromTheHeader() {
        List<string> columns = Fixtures.Parsed.CheckpointBlock.Columns;

        Assert.Equal("Hidden", columns[0]);
        Assert.Equal("WR", columns[1]);
        Assert.Equal("Gold", columns[2]);
        Assert.Equal("Unranked", columns[^1]);
        Assert.All(Fixtures.Imported, segment => Assert.Equal(columns.Count, segment.Times.Count));
        // the Farewell tab stops at Red 3, one column short of the header the
        // merged block took from the A tab: its rows are padded rather than
        // left ragged, or TierComparison would run off the end of them
        SheetSegment farewell = Assert.Single(Fixtures.Imported,
            s => s.Chapter == "Farewell" && s.Name == "Farewell");
        Assert.Equal(TimeSpan.Parse("00:01:18.353"), farewell.Times[1]);
        Assert.Null(farewell.Times[^1]);
    }

    // none of the excluded row families may be imported; the emoji
    // markers themselves never survive Import either — the imported hearts and
    // cassettes are renamed after what they collect. "Wake Up" is excluded for
    // good (owner decision 2026-08-18): those rows time a wake-up animation
    // whose duration is fixed, so there is nothing to compare a run against
    [Theory]
    [InlineData("💙")]
    [InlineData("📼")]
    [InlineData("💎")]
    [InlineData("Clear")]
    [InlineData("Wake Up")]
    [InlineData("3k ")]
    [InlineData("SoB")]
    public void LeavesTheNotYetSupportedRowsOut(string marker) {
        Assert.DoesNotContain(Fixtures.Imported, segment => segment.Name.Contains(marker));
    }

    [Theory]
    [InlineData("1b")]
    [InlineData("7b")]
    [InlineData("8b")]
    [InlineData("Chapter Times")]
    [InlineData("Filetime Buffer")]
    public void LeavesTheNotYetSupportedChaptersOut(string chapter) {
        Assert.DoesNotContain(Fixtures.Imported, segment => segment.Chapter.Contains(chapter));
    }

    // the row table answers on (scope, sheet name) and a name that several
    // chapters share resolves under each of their scopes. SessionBests keys on
    // the anchor, which is what lets a run be re-labelled onto another row of
    // the same checkpoint -- and what makes any caller walking the whole sheet
    // responsible for filtering by chapter first.
    //
    // Pinned because dropping that filter put the Start row of six chapters on
    // the export screen at once, most of them ticked. Seen on 2026-08-30.
    [Fact]
    public void OneCheckpointNameIsSharedByChaptersAndResolvesToOneAnchor() {
        List<string> chapters = [.. SheetRows.All
            .Where(row => row.Name == "Start")
            .Select(row => row.Chapter)
            .Distinct()];

        // several chapters call their first segment "Start" -- that is the
        // premise; if it ever stops being true this test has nothing to say
        Assert.True(chapters.Count > 1, string.Join(", ", chapters));

        // and that one name resolves under each of their scopes, so a caller
        // holding a scope and a name has nothing left that says which chapter
        foreach (string scope in SheetRows.All.Where(row => row.Name == "Start").Select(row => row.Scope).Distinct()) {
            Assert.True(SheetRows.TryFindInScope(scope, "Start", out _), scope);
        }
    }
}
