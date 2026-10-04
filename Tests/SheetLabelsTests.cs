using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// Checks the export half of the row table: SheetLabels is the last hop before
// a time is written, turning srs's own (chapter, name) into the row of the
// player's sheet through SheetRows.
public class SheetLabelsTests {
    [Fact]
    public void EveryImportedSegmentHasARow() {
        List<string> missing = [];
        foreach (SheetRow row in SheetRows.All) {
            if (!SheetLabels.TryMap(row.Chapter, row.Name, out _)) {
                missing.Add($"{row.Chapter}/{row.Name}");
            }
        }

        Assert.Empty(missing);
    }

    // the default target: same label, matching entry tab, the Standards chapter
    // without " CP", empty on Farewell. A failure means a row whose two
    // documents differ, which needs a Target override, not a new default
    [Fact]
    public void EveryRowIsWrittenUnderTheLabelItWasImportedFrom() {
        List<string> wrong = [];
        foreach (SheetRow row in SheetRows.All) {
            if (row.Target == null && SheetRows.TargetOf(row).Cp != row.Label) {
                wrong.Add($"({row.Chapter}, {row.Name}) writes \"{SheetRows.TargetOf(row).Cp}\" but reads \"{row.Label}\"");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // an override marks a real difference between the two documents; none exists yet
    [Fact]
    public void NoRowCarriesAnOverrideYet() {
        Assert.DoesNotContain(SheetRows.All, row => row.Target != null);
    }

    [Fact]
    public void EachRowIsKeyedOnceOnBothSides() {
        Assert.Equal(SheetRows.All.Length,
            SheetRows.All.Select(r => (r.Tab, r.SheetChapter, r.Label)).Distinct().Count());
        Assert.Equal(SheetRows.All.Length,
            SheetRows.All.Select(r => (r.Chapter, r.Name)).Distinct().Count());
    }

    [Fact]
    public void UnknownSegmentIsNotExported() {
        Assert.False(SheetLabels.TryMap("9a", "Nowhere", out _));
        Assert.False(SheetLabels.TryMap("5a", "Depths", out _)); // srs folds 5a/5b
    }

    [Fact]
    public void OnlyTheThreeWritableTabsAreTargeted() {
        HashSet<string> tabs = [.. SheetRows.All.Select(row => SheetRows.TargetOf(row).Tab)];

        Assert.Equal(["A Sides", "B+C Sides", "Farewell"], tabs);
        Assert.Equal("A Sides", SheetLabels.TabASides);
        Assert.Equal("B+C Sides", SheetLabels.TabBCSides);
        Assert.Equal("Farewell", SheetLabels.TabFarewell);
    }

    [Fact]
    public void EveryRowNamesItsGameAnchor() {
        Assert.All(SheetRows.All, row => Assert.False(string.IsNullOrEmpty(row.Anchor)));
        Assert.Equal("Hollows", Row("6a/b", "Hollows Tape").Anchor);
        Assert.Equal("Start", Row("Farewell", "Start DTS").Anchor);
        Assert.Equal("Reflection", Row("6a/b", "Falling").Anchor);
        Assert.Equal("Heart of the Mountain", Row("8a", "HotM Vertical").Anchor);
        Assert.Equal("HotM Horizontal", Row("8a", "HotM Horizontal").Anchor);
        Assert.Equal("500 M", Row("7a", "500m").Anchor);
        Assert.Equal("5b", Row("5a/b", "Mix Master").Scope);
    }

    private static SheetRow Row(string chapter, string name) {
        Assert.True(SheetRows.TryFind(chapter, name, out SheetRow row), $"{chapter}/{name}");
        return row;
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
