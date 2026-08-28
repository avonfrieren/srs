using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// SheetLabels is the last hop before a time is written: it turns srs's own
// (chapter, name) into the row of the player's sheet. Nothing forces it to
// agree with SheetData.Import, so a checkpoint added upstream would import
// fine and silently never export.
public class SheetLabelsTests {
    [Fact]
    public void EveryImportedSegmentHasARow() {
        List<(string, string)> missing = [];
        foreach ((string Chapter, string Name) segment in SheetData.Import.Values) {
            if (!SheetLabels.TryMap(segment.Chapter, segment.Name, out _)) {
                missing.Add(segment);
            }
        }

        Assert.Empty(missing);
    }

    // Import's key half is the row's label as the sheet spells it, and that is
    // exactly what SheetLabels has to write back. Comparing the two puts the
    // whole table behind the fixtures: refresh them, and a renamed row fails
    // here instead of going quietly notFound on the next export. Stubborness
    // sat wrong for months because the only check on it was a pin written from
    // the same reading as the value it pinned.
    //
    // A failure means one of two things. Either srs is behind a rename, and the
    // fix is to follow it on both sides; or the personal tabs genuinely spell a
    // row differently from the standards tabs, which has never happened and
    // would need a snapshot of the personal tab to be checkable at all.
    [Fact]
    public void EveryRowIsWrittenUnderTheLabelItWasImportedFrom() {
        List<string> wrong = [];
        foreach (KeyValuePair<(string Chapter, string Name), (string Chapter, string Name)> entry in SheetData.Import) {
            if (!SheetLabels.TryMap(entry.Value.Chapter, entry.Value.Name, out SheetRowRef row)) {
                continue; // EveryImportedSegmentHasARow is what covers this
            }

            if (row.Cp != entry.Key.Name) {
                wrong.Add($"({entry.Value.Chapter}, {entry.Value.Name}) writes \"{row.Cp}\" "
                          + $"but was imported from \"{entry.Key.Name}\"");
            }
        }

        // not Assert.Empty: it truncates the collection, and a sheet-wide rename
        // puts several rows in here at once
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void UnknownSegmentIsNotExported() {
        Assert.False(SheetLabels.TryMap("9a", "Nowhere", out _));
        Assert.False(SheetLabels.TryMap("5a", "Depths", out _)); // srs folds 5a/5b
    }

    [Fact]
    public void OnlyTheThreeWritableTabsAreTargeted() {
        HashSet<string> tabs = [];
        foreach (SheetRowRef row in SheetLabels.Map.Values) {
            tabs.Add(row.Tab);
        }

        Assert.Equal(["A Sides", "B+C Sides", "Farewell"], tabs);
        Assert.Equal("A Sides", SheetLabels.TAB_A_SIDES);
        Assert.Equal("B+C Sides", SheetLabels.TAB_B_C_SIDES);
        Assert.Equal("Farewell", SheetLabels.TAB_FAREWELL);
    }

    // the chapter echo srs strips from its own names is back on the sheet
    [Theory]
    [InlineData("1a", "Start", "1a", "1a Start")]
    [InlineData("2a", "Start", "2a", "2a Start")]
    [InlineData("3a", "Start", "3a", "3a Start")]
    [InlineData("4a", "Start", "4a", "4a Start")]
    [InlineData("8a", "Start", "8a", "8a Start")]
    public void StartRowsCarryTheChapterEcho(string srsChapter, string srsName, string chapter, string cp) {
        AssertRow(srsChapter, srsName, "A Sides", chapter, cp);
    }

    [Fact]
    public void EmojiRowsUseTheSheetSpelling() {
        AssertRow("3a", "Huge Mess Heart", "A Sides", "3a", "Huge Mess \U0001F499");
        AssertRow("4a", "Shrine Heart", "A Sides", "4a", "Shrine \U0001F499 Clear");
        AssertRow("5a/b", "Depths Tape", "A Sides", "5a", "Depths \U0001F4FC RTM");
        AssertRow("6a/b", "Hollows Tape", "A Sides", "6a", "Hollows \U0001F4FC RTM");
    }

    // the three rows the sheet renamed on 2026-08-28. They were the only places
    // srs's own name and the sheet's label diverged, and they are identities now:
    // an edit putting one of the old spellings back would export nowhere
    [Fact]
    public void RenamedRowsKeepTheSheetsSpelling() {
        AssertRow("5a/b", "Unravelling", "A Sides", "5a", "Unravelling");
        AssertRow("5a/b", "Through the Mirror", "B+C Sides", "5b", "Through the Mirror");
        AssertRow("Farewell", "Stubbornness", "Farewell", "", "Stubbornness");
    }

    // the folded chapters split across the two side tabs
    [Fact]
    public void FoldedChaptersSplitBySide() {
        AssertRow("5a/b", "5a Start", "A Sides", "5a", "5a Start");
        AssertRow("5a/b", "Mix Master", "B+C Sides", "5b", "Mix Master");
        AssertRow("6a/b", "6a Rock Bottom", "A Sides", "6a", "Rock Bottom");
        AssertRow("6a/b", "6b Rock Bottom", "B+C Sides", "6b", "Rock Bottom");
    }

    [Fact]
    public void FarewellRowsHaveNoChapter() {
        AssertRow("Farewell", "Start", "Farewell", "", "Start");
        AssertRow("Farewell", "Determination DTS", "Farewell", "", "Determination DTS");
        AssertRow("Farewell", "Reconciliation", "Farewell", "", "Reconciliation");
    }

    private static void AssertRow(string srsChapter, string srsName, string tab, string chapter, string cp) {
        Assert.True(SheetLabels.TryMap(srsChapter, srsName, out SheetRowRef row));
        Assert.Equal(new SheetRowRef(tab, chapter, cp), row);
    }
}
