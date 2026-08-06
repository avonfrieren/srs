using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// The mod addresses checkpoints by name across three hardcoded tables that
// nothing forces to agree: SheetData.Import (which sheet rows to keep and what
// to call them), RoomCounts.Counts (when a run is finished) and
// SegmentAutoDetect.CheckpointMap (what the played checkpoint selects). A
// rename on either side degrades silently — the checkpoint just disappears
// from the sliders, or the room count quietly falls back to 99. These tests
// cross-check the three tables against each other and against the sheet.
public class SheetConsistencyTests {
    // every raw (chapter, checkpoint) pair present in the exported tabs
    private static readonly HashSet<(string, string)> RawRows = [
        .. new[] { Fixtures.ASides, Fixtures.BSides }
            .SelectMany(SheetData.ParseBlocks)
            .SelectMany(block => block.Segments)
            .Select(segment => (segment.Chapter, segment.Name))
    ];

    // checkpoints the sheet has no room count for: the last checkpoint of each
    // chapter (the run ends with the chapter itself) and the Prologue. They get
    // RoomCounts.Unknown on purpose — listing them here means an accidental
    // fallback to Unknown, caused by a typo or a rename, fails the test instead
    private static readonly HashSet<(string, string)> ExpectedUnknown = [
        ("Prologue", "Granny"),
        ("1a", "Chasm"),
        ("2a", "Awake"),
        ("3a", "Presidential Suite"),
        ("4a", "Cliff Face"),
        ("5a/b", "Mix Master"),
        ("6a/b", "Resolution"),
        ("6a/b", "Reprieve"),
        ("7a", "3000m"),
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

    // 2. every checkpoint the mod imports has a room count, or is a deliberate
    // Unknown. Without this, Import saying "0m" while RoomCounts still says
    // "Start" would just make the timer never complete
    [Fact]
    public void EveryImportedCheckpointHasAKnownRoomCountOrIsExplicitlyUnknown() {
        HashSet<(string, string)> unknown = [
            .. Fixtures.Imported
                .Where(segment => RoomCounts.TargetFor(segment) == RoomCounts.Unknown)
                .Select(segment => (segment.Chapter, segment.Name))
        ];

        Assert.Equal(ExpectedUnknown, unknown);
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
    // emoji rows kept, and both are manual-only (they start at the same
    // in-game checkpoint as their non-cassette sibling)
    [Theory]
    [InlineData("5a/b", "Depths Tape", 8)]
    [InlineData("6a/b", "Hollows Tape", 2)]
    public void ImportsTheCassetteCheckpointsWithTheirRoomCount(string chapter, string name, int rooms) {
        SheetSegment segment = Assert.Single(Fixtures.Imported,
            s => s.Chapter == chapter && s.Name == name);

        Assert.Equal(rooms, RoomCounts.TargetFor(segment));
        Assert.DoesNotContain(name, SegmentAutoDetect.CheckpointMap.Values);
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
