using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// EndConditionOf / CategoryOf read the sheet's naming vocabulary off the raw
// row names. The sheet's own spacing is inconsistent ("📼 Clear" and
// "📼Clear" both exist), so the derivation must tolerate it — these are the
// rules the phase 6 refactor rests on.
public class EndConditionTests {
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
    [InlineData("Shrine 💙 RTM")]
    public void HeartRtmRowsEndAtTheHeart(string rawName) {
        Assert.Equal(EndCondition.Heart, SheetData.EndConditionOf(rawName));
    }

    // everything that is not an RTM row runs to the end of its segment, and
    // that includes the "Clear" ones: on a checkpoint row the suffix means
    // "collect it and keep going", not the chapter's completion — the sheet's
    // own times say so (Shrine 💙 Clear is 27.5s, the two checkpoints after
    // Shrine are 78s on their own). Whether a segment's end is a checkpoint
    // or the chapter running out is runtime knowledge, not the parser's
    [Theory]
    [InlineData("Granny")]
    [InlineData("Intervention")]
    [InlineData("Awake")]
    [InlineData("3000m")]
    [InlineData("Stubbornness")]
    [InlineData("Start DTS")]
    [InlineData("Crossing 💙")]
    [InlineData("Shrine 💙 Clear")]
    [InlineData("Hollows 📼Clear")]
    [InlineData("1b Clear")]
    public void EveryOtherRowEndsWithItsSegment(string rawName) {
        Assert.Equal(EndCondition.Checkpoint, SheetData.EndConditionOf(rawName));
    }

    [Theory]
    [InlineData("Depths 📼 RTM", SegmentCategory.Cassette)]
    [InlineData("Hollows 📼Clear", SegmentCategory.Cassette)]
    [InlineData("Depths", SegmentCategory.AnyPercent)]
    [InlineData("Crossing 💙", SegmentCategory.TrueEnding)]
    [InlineData("Shrine 💙 Clear", SegmentCategory.TrueEnding)]
    [InlineData("💙+📼 RTM", SegmentCategory.Cassette)] // combined rows still default to the cassette
    public void TheCategoryComesFromTheMarker(string rawName, SegmentCategory expected) {
        Assert.Equal(expected, SheetData.CategoryOf(rawName));
    }

    // Farewell's double-dash skip has no emoji: the sheet suffixes the six
    // segments it covers instead. The suffix must not catch the tab's "DTS
    // SoB"/"DTS IL" totals, which only start with it
    [Theory]
    [InlineData("Start DTS", SegmentCategory.TrueEndingDts)]
    [InlineData("Determination DTS", SegmentCategory.TrueEndingDts)]
    [InlineData("DTS SoB", SegmentCategory.AnyPercent)]
    [InlineData("DTS IL", SegmentCategory.AnyPercent)]
    [InlineData("Stubbornness", SegmentCategory.AnyPercent)]
    public void TheDtsSuffixIsTheSkipsMarker(string rawName, SegmentCategory expected) {
        Assert.Equal(expected, SheetData.CategoryOf(rawName));
    }
}
