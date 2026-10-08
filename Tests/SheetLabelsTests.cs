using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// Checks the export half of the row table: SheetLabels is the last hop before
// a time is written, turning srs's own (chapter, name) into the row of the
// player's sheet through SheetRows.
public class SheetLabelsTests {
    // a row whose two documents differ carries a Target, which wins over the
    // default; the default's tab and chapter are pinned row by row below
    [Fact]
    public void ATargetOverridesTheDefault() {
        SheetRowRef elsewhere = new("A Sides", "1a", "Crossing (renamed)");
        SheetRow overridden = Row("1a", "Crossing") with { Target = elsewhere };

        Assert.Equal(elsewhere, SheetRows.TargetOf(overridden));
    }

    // the entry tab files an A-side IL under the bare chapter, not under "1a IL"
    [Fact]
    public void AnIlIsWrittenUnderItsBareChapter() {
        SheetRow[] ils = [.. SheetRows.All.Where(row => row.SheetChapter.EndsWith(" IL"))];

        Assert.Equal(8, ils.Length);
        Assert.All(ils, row => Assert.Equal(new SheetRowRef("A Sides", row.Scope, "Clear"), SheetRows.TargetOf(row)));
    }

    [Fact]
    public void FarewellsIlsAreWrittenWithoutTheirSuffix() {
        AssertRow("Farewell", "DTS IL", "Farewell", "", "DTS");
        AssertRow("Farewell", "No DTS IL", "Farewell", "", "No DTS");
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
    [InlineData("1a", "1a Start")]
    [InlineData("2a", "2a Start")]
    [InlineData("3a", "3a Start")]
    [InlineData("4a", "4a Start")]
    [InlineData("8a", "8a Start")]
    public void StartRowsCarryTheChapterEcho(string chapter, string cp) {
        AssertRow(chapter, "Start", "A Sides", chapter, cp);
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

    // the C-side band has no label column: the sheet matches the chapter twice
    [Fact]
    public void ACSideIsWrittenUnderItsChapter() {
        AssertRow("1c", "1c", "B+C Sides", "1c", "1c");
        AssertRow("8c", "8c", "B+C Sides", "8c", "8c");
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
