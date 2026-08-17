using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// The mod addresses checkpoints by name across three hardcoded tables that
// nothing forces to agree: SheetData.Import (which sheet rows to keep and what
// to call them), SegmentAutoDetect.CheckpointMap (what the played checkpoint
// selects) and SegmentAutoDetect.CategoryVariants (which variant the Category
// setting resolves). A rename on either side degrades silently — the
// checkpoint just disappears from the sliders, or auto-detection stops moving.
// These tests cross-check the tables against each other and against the sheet.
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

    // 1. the allowlist still matches the sheet. This is the test that catches a
    // rename on the sheet's side: refresh Tests/Fixtures/*.csv, and any row the
    // mod expects that no longer exists shows up here by name
    [Fact]
    public void EveryImportedRowStillExistsInTheSheet() {
        List<(string, string)> missing = SheetData.Import.Keys
            .Where(key => !RawRows.Contains(key))
            .ToList();

        Assert.Empty(missing);
    }

    // 2. every imported segment carries the end condition its raw sheet name
    // declares: the two 📼 RTM rows end at the cassette collect, everything
    // else — the two hearts included, their rows are not RTM ones — ends at
    // the next in-game checkpoint (or the chapter's completion when there is
    // none, resolved at runtime, no table for it). A marker slipping through
    // Import unnoticed would silently mistime the segment
    [Fact]
    public void EveryImportedSegmentEndsTheWayItsRawNameDeclares() {
        HashSet<(string, string)> cassette = [
            .. Fixtures.Imported
                .Where(segment => segment.End == EndCondition.Cassette)
                .Select(segment => (segment.Chapter, segment.Name))
        ];

        Assert.Equal([("5a/b", "Depths Tape"), ("6a/b", "Hollows Tape")], cassette);
        Assert.All(Fixtures.Imported.Where(segment => segment.End != EndCondition.Cassette),
            segment => Assert.Equal(EndCondition.Checkpoint, segment.End));
    }

    // 2bis. every imported segment resolves back to a game checkpoint through
    // GameNameOf — that anchor is what RunWatcher uses for both the start
    // guard and the end room of Checkpoint segments. The scopes are the ones
    // CheckpointMap itself uses; a segment resolving in none of them means a
    // broken rename between the tables
    [Fact]
    public void EveryImportedSegmentIsAnchoredToAGameCheckpoint() {
        HashSet<string> scopes = [.. SegmentAutoDetect.CheckpointMap.Keys.Select(key => key.Scope)];

        List<string> unanchored = Fixtures.Imported
            .Select(segment => segment.Name)
            .Where(name => !scopes.Any(scope => SegmentAutoDetect.GameNameOf(scope, name) != null))
            .ToList();

        Assert.Empty(unanchored);
        // the variants inherit their plain sibling's anchor
        Assert.Equal("Depths", SegmentAutoDetect.GameNameOf("5a", "Depths Tape"));
        Assert.Equal("Hollows", SegmentAutoDetect.GameNameOf("6a", "Hollows Tape"));
        Assert.Equal("Huge Mess", SegmentAutoDetect.GameNameOf("3a", "Huge Mess Heart"));
        Assert.Equal("Start", SegmentAutoDetect.GameNameOf("Farewell", "Start DTS"));
        Assert.Equal("Start", SegmentAutoDetect.GameNameOf("7a", "0m"));
        // and the sheet's spelling of Farewell's seventh checkpoint is not the
        // game's, which is exactly what the table is for
        Assert.Equal("Stubbornness", SegmentAutoDetect.GameNameOf("Farewell", "Stubborness"));
    }

    // 2ter. every start-room override names a (scope, game checkpoint) pair
    // CheckpointMap actually knows. An override keyed on a checkpoint no table
    // anchors would never fire — and, worse, the previous segment would keep
    // ending at the checkpoint's own room, silently overlapping the next one
    [Fact]
    public void EveryStartRoomOverrideTargetsAKnownCheckpoint() {
        List<(string, string)> unknown = SegmentAutoDetect.StartRoomOverrides.Keys
            .Where(key => !SegmentAutoDetect.CheckpointMap.ContainsKey(key))
            .ToList();

        Assert.Empty(unknown);
        // and the reverse lookup the auto-detection relies on agrees, scope
        // included: end_0 is 2A's Awake, and nothing at all in 7A
        Assert.Equal("Awake", SegmentAutoDetect.OverriddenCheckpointAt("2a", "end_0"));
        Assert.Equal("Start", SegmentAutoDetect.OverriddenCheckpointAt("7a", "a-00"));
        Assert.Null(SegmentAutoDetect.OverriddenCheckpointAt("7a", "end_0"));
        Assert.Null(SegmentAutoDetect.OverriddenCheckpointAt("2a", "3"));
        // the virtual checkpoint's room is what makes it detectable at all:
        // the game has no checkpoint there to notice
        Assert.Equal("HotM Horizontal", SegmentAutoDetect.OverriddenCheckpointAt("8a", "d-08"));
    }

    // 2ter bis. a checkpoint the sheet cuts in two names a real game
    // checkpoint on one side and a virtual one on the other, and the virtual
    // half must have a room: it is both where its own run starts and where the
    // first half's run ends, so a missing room would silently let the first
    // half run to the end of the chapter
    [Fact]
    public void EverySplitCheckpointHasBothHalvesAnchored() {
        foreach (KeyValuePair<(string Scope, string GameName), string> entry
                 in SegmentAutoDetect.SplitCheckpoints) {
            Assert.Contains(entry.Key, SegmentAutoDetect.CheckpointMap.Keys);
            Assert.Contains((entry.Key.Scope, entry.Value), SegmentAutoDetect.CheckpointMap.Keys);
            Assert.Contains((entry.Key.Scope, entry.Value), SegmentAutoDetect.StartRoomOverrides.Keys);
        }

        Assert.Equal("HotM Horizontal", SegmentAutoDetect.SplitCheckpoints[("8a", "Heart of the Mountain")]);
        Assert.Equal("d-08", SegmentAutoDetect.StartRoomOverrides[("8a", "HotM Horizontal")]);
    }

    // 2quater. same for the untimed head added back to the captured time: an
    // entry keyed on a checkpoint no table anchors would never be added, and
    // the segment would be compared against thresholds that include a part of
    // it — silently several tiers too high. The value is pinned: it is the
    // sheet's own constant, not something derivable from the game
    [Fact]
    public void EveryUntimedHeadTargetsAKnownCheckpointAndKeepsItsValue() {
        List<(string, string)> unknown = SegmentAutoDetect.UntimedSegmentHead.Keys
            .Where(key => !SegmentAutoDetect.CheckpointMap.ContainsKey(key))
            .ToList();

        Assert.Empty(unknown);
        Assert.Equal(TimeSpan.FromMilliseconds(5508), SegmentAutoDetect.UntimedSegmentHead[("7a", "Start")]);
        // and it only concerns the segments that have an override start room
        Assert.All(SegmentAutoDetect.UntimedSegmentHead.Keys,
            key => Assert.True(SegmentAutoDetect.StartRoomOverrides.ContainsKey(key)));
    }

    // 3. auto-detection only points at checkpoints that were actually imported;
    // a stale name here silently stops the detection on that checkpoint
    [Fact]
    public void EveryAutoDetectedNameExistsAmongTheImportedCheckpoints() {
        HashSet<string> importedNames = [.. Fixtures.Imported.Select(segment => segment.Name)];

        List<string> dangling = SegmentAutoDetect.CheckpointMap.Values
            .Where(name => !importedNames.Contains(name))
            .Distinct()
            .ToList();

        Assert.Empty(dangling);
    }

    // 4. (chapter, name) is the address used by the settings, the sliders and
    // both other tables, so two sheet rows must never collapse onto one
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
        Assert.Equal(SheetData.Import.Count, Fixtures.Parsed.SegmentCount);
        Assert.Equal(
            ["Prologue", "1a", "2a", "3a", "4a", "5a/b", "6a/b", "7a", "8a", "Farewell"],
            Fixtures.Parsed.CheckpointBlock.Chapters());
    }

    // the two cassette routes the owner asked for in v2.0.0; they are the only
    // emoji rows kept. They start at the same in-game checkpoint as their
    // non-cassette sibling, so they are never in CheckpointMap — the Category
    // setting resolves them through CategoryVariants instead — and their runs
    // end at the cassette collect, not in any room
    [Theory]
    [InlineData("5a/b", "Depths Tape")]
    [InlineData("6a/b", "Hollows Tape")]
    public void ImportsTheCassetteCheckpointsEndingAtTheCollect(string chapter, string name) {
        SheetSegment segment = Assert.Single(Fixtures.Imported,
            s => s.Chapter == chapter && s.Name == name);

        Assert.Equal(EndCondition.Cassette, segment.End);
        Assert.DoesNotContain(name, SegmentAutoDetect.CheckpointMap.Values);
    }

    // 6. the category overlay resolves plain names produced by CheckpointMap
    // into imported rows of the same chapter — a rename on either end would
    // silently turn the variant back into its plain sibling. The plain row is
    // always the any% one (that is what "a category adds to any%" means) and
    // the variant never is, or the overlay would be resolving a row onto itself
    [Fact]
    public void EveryCategoryVariantTargetsAnImportedRowOfTheSameChapter() {
        foreach (KeyValuePair<(SegmentCategory Category, string Chapter, string SheetName), string> entry
                 in SegmentAutoDetect.CategoryVariants) {
            SheetSegment plain = Assert.Single(Fixtures.Imported,
                s => s.Chapter == entry.Key.Chapter && s.Name == entry.Key.SheetName);
            SheetSegment variant = Assert.Single(Fixtures.Imported,
                s => s.Chapter == entry.Key.Chapter && s.Name == entry.Value);

            Assert.Contains(entry.Key.SheetName, SegmentAutoDetect.CheckpointMap.Values);
            Assert.Equal(plain.Chapter, variant.Chapter);
            Assert.Equal(SegmentCategory.AnyPercent, plain.Category);
            Assert.NotEqual(SegmentCategory.AnyPercent, variant.Category);
        }
    }

    // 7. the category read off the raw sheet names matches the overlay: an
    // imported marked row absent from CategoryVariants would never be
    // selectable by auto-detection again (nothing else points at it)
    [Fact]
    public void EveryMarkedRowIsReachableThroughTheCategoryOverlay() {
        HashSet<string> marked = [
            .. Fixtures.Imported
                .Where(segment => segment.Category != SegmentCategory.AnyPercent)
                .Select(segment => segment.Name)
        ];

        HashSet<string> variants = [.. SegmentAutoDetect.CategoryVariants.Values];

        Assert.Equal(marked, variants);
        Assert.Contains("Hollows Tape", marked);
        Assert.Contains("Shrine Heart", marked);
        Assert.Contains("Determination DTS", marked);
    }

    // 7bis. the whole point of the category slider: no category may hold two
    // segments starting at the same in-game checkpoint, or the auto-detection
    // would have to guess between them. Checked per (category, chapter,
    // anchor), the anchor being the game checkpoint the segment is timed from
    [Fact]
    public void NoCategoryHasTwoSegmentsOnTheSameCheckpoint() {
        HashSet<string> scopes = [.. SegmentAutoDetect.CheckpointMap.Keys.Select(key => key.Scope)];

        foreach (SegmentCategory category in Enum.GetValues<SegmentCategory>()) {
            List<(string Chapter, string Anchor)> anchors = Fixtures.Imported
                .Where(segment => SelectedIn(category, segment))
                .Select(segment => (segment.Chapter, Anchor: scopes
                    .Select(scope => scope + "/" + SegmentAutoDetect.GameNameOf(scope, segment.Name))
                    .First(anchor => !anchor.EndsWith("/", StringComparison.Ordinal))))
                .ToList();

            Assert.Equal(anchors.Distinct().Count(), anchors.Count);
        }
    }

    // 7ter. what the Category slider is for, spelled out on the checkpoints
    // that exist in several versions: the same in-game checkpoint, four
    // categories, four answers (a category with nothing to say keeps the any%
    // row). This is the table SegmentAutoDetect.Apply reads on every frame
    [Theory]
    [InlineData(SegmentCategory.AnyPercent, "6a/b", "Hollows", "Hollows")]
    [InlineData(SegmentCategory.Cassette, "6a/b", "Hollows", "Hollows Tape")]
    [InlineData(SegmentCategory.TrueEnding, "6a/b", "Hollows", "Hollows")]
    [InlineData(SegmentCategory.AnyPercent, "3a", "Huge Mess", "Huge Mess")]
    [InlineData(SegmentCategory.TrueEnding, "3a", "Huge Mess", "Huge Mess Heart")]
    [InlineData(SegmentCategory.TrueEndingDts, "3a", "Huge Mess", "Huge Mess Heart")]
    [InlineData(SegmentCategory.TrueEnding, "Farewell", "Start", "Start")]
    [InlineData(SegmentCategory.TrueEndingDts, "Farewell", "Start", "Start DTS")]
    // the skip is over by Stubborness: both True Ending categories run it
    [InlineData(SegmentCategory.TrueEndingDts, "Farewell", "Stubborness", "Stubborness")]
    public void TheCategoryResolvesTheVariantOfTheCheckpoint(
        SegmentCategory category, string chapter, string plain, string expected) {
        string resolved = SegmentAutoDetect.CategoryVariants
            .TryGetValue((category, chapter, plain), out string variant)
            ? variant
            : plain;

        Assert.Equal(expected, resolved);
        Assert.Contains(Fixtures.Imported, s => s.Chapter == chapter && s.Name == resolved);
    }

    // the segments a category actually selects: its own variants, plus every
    // any% row it does not override
    private static bool SelectedIn(SegmentCategory category, SheetSegment segment) {
        bool isVariantOfThisCategory = SegmentAutoDetect.CategoryVariants
            .Any(entry => entry.Key.Category == category && entry.Value == segment.Name);
        bool isOverridden = SegmentAutoDetect.CategoryVariants
            .ContainsKey((category, segment.Chapter, segment.Name));

        return isVariantOfThisCategory
               || (segment.Category == SegmentCategory.AnyPercent && !isOverridden);
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

    // none of the excluded row families may leak into the sliders; the emoji
    // markers themselves never survive Import either — the imported hearts and
    // cassettes are renamed after what they collect
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
}
