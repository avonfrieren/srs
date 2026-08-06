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
    // every raw (chapter, checkpoint) pair present in the exported tabs
    private static readonly HashSet<(string, string)> RawRows = [
        .. new[] { Fixtures.ASides, Fixtures.BSides }
            .SelectMany(SheetData.ParseBlocks)
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
    // else ends at the next in-game checkpoint (or the chapter's completion
    // when there is none — resolved at runtime, no table for it). A marker
    // slipping through Import unnoticed would silently mistime the segment
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
        // the two variants inherit their plain sibling's anchor
        Assert.Equal("Depths", SegmentAutoDetect.GameNameOf("5a", "Depths Tape"));
        Assert.Equal("Hollows", SegmentAutoDetect.GameNameOf("6a", "Hollows Tape"));
        Assert.Equal("Start", SegmentAutoDetect.GameNameOf("7a", "0m"));
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
            ["Prologue", "1a", "2a", "3a", "4a", "5a/b", "6a/b", "7a"],
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
    // into rows of its own category, inside the same chapter — a rename on
    // either end would silently turn the variant back into its plain sibling
    [Fact]
    public void EveryCategoryVariantTargetsAnImportedRowOfItsCategory() {
        foreach (KeyValuePair<(SegmentCategory Category, string SheetName), string> entry
                 in SegmentAutoDetect.CategoryVariants) {
            SheetSegment plain = Assert.Single(Fixtures.Imported, s => s.Name == entry.Key.SheetName);
            SheetSegment variant = Assert.Single(Fixtures.Imported, s => s.Name == entry.Value);

            Assert.Contains(entry.Key.SheetName, SegmentAutoDetect.CheckpointMap.Values);
            Assert.Equal(plain.Chapter, variant.Chapter);
            Assert.Equal(SegmentCategory.AnyPercent, plain.Category);
            Assert.Equal(entry.Key.Category, variant.Category);
        }
    }

    // 7. the category read off the raw sheet names matches the overlay: an
    // imported 📼 row absent from CategoryVariants would never be selectable
    // by auto-detection again (nothing else points at it)
    [Fact]
    public void EveryCassetteRowIsReachableThroughTheCategoryOverlay() {
        HashSet<string> cassette = [
            .. Fixtures.Imported
                .Where(segment => segment.Category == SegmentCategory.Cassette)
                .Select(segment => segment.Name)
        ];

        HashSet<string> variants = [
            .. SegmentAutoDetect.CategoryVariants
                .Where(entry => entry.Key.Category == SegmentCategory.Cassette)
                .Select(entry => entry.Value)
        ];

        Assert.Equal(["Depths Tape", "Hollows Tape"], cassette.OrderBy(name => name).ToList());
        Assert.Equal(cassette, variants);
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
    }

    // none of the excluded row families may leak into the sliders
    [Theory]
    [InlineData("💙")]
    [InlineData("💎")]
    [InlineData("Clear")]
    [InlineData("Wake Up")]
    [InlineData("3k ")]
    public void LeavesTheNotYetSupportedRowsOut(string marker) {
        Assert.DoesNotContain(Fixtures.Imported, segment => segment.Name.Contains(marker));
    }

    [Theory]
    [InlineData("8a")]
    [InlineData("1b")]
    [InlineData("7b")]
    [InlineData("Chapter Times")]
    [InlineData("Filetime Buffer")]
    public void LeavesTheNotYetSupportedChaptersOut(string chapter) {
        Assert.DoesNotContain(Fixtures.Imported, segment => segment.Chapter.Contains(chapter));
    }
}
