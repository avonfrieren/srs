using System;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// SegmentCategories is the single source of the category labels (Mod Options
// slider + the cycle hotkey's popup) and of the cycling order. The silent
// failure it guards against: adding a category to the enum without its label,
// which would show the raw enum name in game.
public class CategoryTests {
    [Fact]
    public void EveryCategoryHasALabel() {
        Assert.Equal(Enum.GetValues(typeof(SegmentCategory)).Length, SegmentCategories.Names.Length);
        foreach (SegmentCategory category in Enum.GetValues<SegmentCategory>()) {
            Assert.Equal(SegmentCategories.Names[(int)category], SegmentCategories.NameOf(category));
        }
    }

    // the labels are the sheet's vocabulary, not translated: pin them so a
    // rename is a deliberate edit here too
    [Fact]
    public void LabelsAreTheSheetVocabulary() {
        Assert.Equal("Any%", SegmentCategories.NameOf(SegmentCategory.AnyPercent));
        Assert.Equal("Any% Cassettes", SegmentCategories.NameOf(SegmentCategory.Cassette));
        Assert.Equal("True Ending", SegmentCategories.NameOf(SegmentCategory.TrueEnding));
        Assert.Equal("True Ending DTS", SegmentCategories.NameOf(SegmentCategory.TrueEndingDts));
    }

    // the hotkey walks the slider's order and wraps back to the first category
    [Fact]
    public void CycleWalksEveryCategoryAndWrapsAround() {
        int count = Enum.GetValues(typeof(SegmentCategory)).Length;
        SegmentCategory category = SegmentCategory.AnyPercent;
        for (int i = 1; i < count; i++) {
            category = SegmentCategories.Next(category);
            Assert.Equal((SegmentCategory)i, category);
        }

        Assert.Equal(SegmentCategory.AnyPercent, SegmentCategories.Next(category));
    }
}
