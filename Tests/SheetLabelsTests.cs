using System.Linq;
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

public class SheetRouteTests {
    // the sheet's own catalogue, from SoB Rankings, limited to the two
    // categories that are stable there
    [Fact]
    public void CoversTheRoutesTheSheetNames() {
        Assert.Equal(["Any%", "True Ending"], SheetRoutes.Categories);
        Assert.Equal(["5a6a", "5b6a", "5b6b", "All"],
            SheetRoutes.Of("Any%").Select(route => route.Name));
        Assert.Equal(["2a \U0001F499 No DTS", "2a \U0001F499 DTS", "6b No DTS", "6b DTS", "All"],
            SheetRoutes.Of("True Ending").Select(route => route.Name));
    }

    // every checkpoint a route plays must be a segment the import produced, or
    // the view would filter the row out and show a chapter with a hole in it
    [Fact]
    public void EveryCheckpointARoutePlaysWasImported() {
        HashSet<string> imported = [.. Fixtures.Imported.Select(segment => segment.Name)];

        foreach (SheetRoute route in SheetRoutes.All.Where(r => r.FiltersRows)) {
            foreach ((string scope, string[] names) in route.ByScope) {
                foreach (string name in names) {
                    Assert.True(imported.Contains(name),
                        $"{route.Category} {route.Name} plays \"{name}\" in {scope}, which the import does not produce");
                }
            }
        }
    }

    // 5b6a and 5b6b differ in 6A alone, and the difference is a truncation:
    // 5b6b stops at the cassette and goes to 6B instead of finishing 6A
    [Fact]
    public void RoutesDifferWhereTheSheetSaysTheyDo() {
        SheetRoute a6 = SheetRoutes.Of("Any%").Single(route => route.Name == "5b6a");
        SheetRoute b6 = SheetRoutes.Of("Any%").Single(route => route.Name == "5b6b");

        Assert.Equal(["5a Start", "Depths Tape"], a6.Checkpoints("5a"));
        Assert.Equal(["5a Start", "Depths Tape"], b6.Checkpoints("5a"));
        Assert.Contains("Resolution", a6.Checkpoints("6a"));
        Assert.Equal(["6a Start", "Lake", "Hollows Tape"], b6.Checkpoints("6a"));
        Assert.False(a6.Covers("6b"));
        Assert.True(b6.Covers("6b"));
    }

    // the chapter is entered twice on a 2a heart route: the heart visit first,
    // then the chapter itself
    [Fact]
    public void TheHeartRouteEntersChapterTwoTwice() {
        SheetRoute heart = SheetRoutes.Of("True Ending").Single(route => route.Name == "2a \U0001F499 DTS");
        SheetRoute sixB = SheetRoutes.Of("True Ending").Single(route => route.Name == "6b DTS");

        Assert.Equal(["Start Heart", "Start", "Intervention", "Awake"], heart.Checkpoints("2a"));
        Assert.Equal(["Start", "Intervention", "Awake"], sixB.Checkpoints("2a"));
    }

    // DTS swaps Farewell's first six segments and nothing else
    [Fact]
    public void DtsChangesOnlyFarewell() {
        SheetRoute plain = SheetRoutes.Of("True Ending").Single(route => route.Name == "6b No DTS");
        SheetRoute dts = SheetRoutes.Of("True Ending").Single(route => route.Name == "6b DTS");

        Assert.Contains("Determination", plain.Checkpoints("Farewell"));
        Assert.Contains("Determination DTS", dts.Checkpoints("Farewell"));
        foreach (string scope in new[] { "1a", "2a", "3a", "4a", "5a", "5b", "6a", "6b", "7a", "8a" }) {
            Assert.Equal(plain.Checkpoints(scope), dts.Checkpoints(scope));
        }
    }

    // a category's "All" is every checkpoint its own routes play, and no more:
    // a row no route of the category visits belongs to another category
    [Fact]
    public void TheAllRouteIsTheUnionOfItsCategory() {
        SheetRoute anyAll = SheetRoutes.Of("Any%").Single(route => route.Name == SheetRoutes.AllRoutes);

        // 2A's heart row is True Ending's, and no Any% route plays it
        Assert.Equal(["Start", "Intervention", "Awake"], anyAll.Checkpoints("2a"));
        // both 5A variants, because 5a6a plays the chapter and 5b6a stops at the cassette
        Assert.Contains("Depths", anyAll.Checkpoints("5a"));
        Assert.Contains("Depths Tape", anyAll.Checkpoints("5a"));
        // 6B is in the union because 5b6b goes there
        Assert.True(anyAll.Covers("6b"));
        // Farewell is in no Any% route
        Assert.False(anyAll.Covers("Farewell"));

        SheetRoute endingAll = SheetRoutes.Of("True Ending").Single(route => route.Name == SheetRoutes.AllRoutes);
        // the union keeps the order the routes play in, so the heart visit
        // stays ahead of the chapter it precedes
        Assert.Equal(["Start Heart", "Start", "Intervention", "Awake"], endingAll.Checkpoints("2a"));
        Assert.Contains("Determination", endingAll.Checkpoints("Farewell"));
        Assert.Contains("Determination DTS", endingAll.Checkpoints("Farewell"));
    }
}
