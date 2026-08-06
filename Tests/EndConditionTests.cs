using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// EndConditionOf / CategoryOf read the sheet's naming vocabulary off the raw
// row names. The sheet's own spacing is inconsistent ("📼 Clear" and
// "📼Clear" both exist), so the derivation must tolerate it — these are the
// rules the phase 6 refactor rests on.
public class EndConditionTests {
    // a "Clear" suffix always means the chapter's completion, marker or not
    [Theory]
    [InlineData("1b Clear")]
    [InlineData("📼 Clear")]
    [InlineData("📼Clear")]
    [InlineData("Hollows 📼Clear")]
    [InlineData("💙+📼Clear")]
    [InlineData("Shrine 💙 Clear")]
    public void ClearRowsEndWithTheChapter(string rawName) {
        Assert.Equal(EndCondition.ChapterComplete, SheetData.EndConditionOf(rawName));
    }

    // RTM rows end the moment the item is collected (community convention:
    // the menuing after the grab is never timed by the room timer)
    [Theory]
    [InlineData("Depths 📼 RTM")]
    [InlineData("Hollows 📼 RTM")]
    [InlineData("📼RTM")]
    [InlineData("💙+📼 RTM")] // combined rows default to the cassette
    public void CassetteRtmRowsEndAtTheCassette(string rawName) {
        Assert.Equal(EndCondition.Cassette, SheetData.EndConditionOf(rawName));
    }

    [Theory]
    [InlineData("2a Start 💙 RTM")]
    [InlineData("Crossing 💙")]
    public void HeartRowsEndAtTheHeart(string rawName) {
        Assert.Equal(EndCondition.Heart, SheetData.EndConditionOf(rawName));
    }

    // plain rows end at the next in-game checkpoint; whether one exists (and
    // which room it starts in) is runtime knowledge, not the parser's
    [Theory]
    [InlineData("Granny")]
    [InlineData("Intervention")]
    [InlineData("Awake")]
    [InlineData("3000m")]
    public void PlainRowsEndAtTheNextCheckpoint(string rawName) {
        Assert.Equal(EndCondition.Checkpoint, SheetData.EndConditionOf(rawName));
    }

    [Theory]
    [InlineData("Depths 📼 RTM", SegmentCategory.Cassette)]
    [InlineData("Hollows 📼Clear", SegmentCategory.Cassette)]
    [InlineData("Depths", SegmentCategory.AnyPercent)]
    [InlineData("Crossing 💙", SegmentCategory.AnyPercent)] // heart category comes later
    public void TheCategoryComesFromTheMarker(string rawName, SegmentCategory expected) {
        Assert.Equal(expected, SheetData.CategoryOf(rawName));
    }
}
