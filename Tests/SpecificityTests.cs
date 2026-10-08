using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// what the HUD and the export show when several rows close on one frame
public class SpecificityTests {
    private static SegmentRule Rule(Collectibles requires, bool berries = false, int order = 0,
        EndKind end = EndKind.NextStart) =>
        new("3a", "3a", $"r{order}", "Huge Mess", StartKind.Room, StartSetup.NextRoom, end,
            Collectibles.None, requires, berries, null, 0, 0, order);

    private static int MostSpecific(params SegmentRule[] rules) => Specificity.MostSpecific(rules, wholeChapter: false);

    // a whole chapter and the segment ending with it are picked apart, so both are shown
    [Fact]
    public void WholeChaptersAndSegmentsAreChosenApart() {
        SegmentRule last = Rule(Collectibles.None, order: 0);
        SegmentRule lastWithTape = Rule(Collectibles.Cassette, order: 1);
        SegmentRule il = Rule(Collectibles.None, order: 2, end: EndKind.ChapterEnd);
        SegmentRule ilWithHeart = Rule(Collectibles.Heart, order: 3, end: EndKind.ChapterEnd);
        SegmentRule[] closed = [last, lastWithTape, il, ilWithHeart];

        Assert.Equal(1, Specificity.MostSpecific(closed, wholeChapter: false));
        Assert.Equal(3, Specificity.MostSpecific(closed, wholeChapter: true));
        Assert.Equal(-1, Specificity.MostSpecific([last], wholeChapter: true));
        Assert.Equal(-1, Specificity.MostSpecific([il], wholeChapter: false));
    }

    [Fact]
    public void TheRowWhoseRequirementsContainTheOthersWins() {
        SegmentRule plain = Rule(Collectibles.None, order: 0);
        SegmentRule heart = Rule(Collectibles.Heart, order: 1);
        SegmentRule arb = Rule(Collectibles.Heart, berries: true, order: 2);

        Assert.Equal(2, MostSpecific(plain, heart, arb));
        Assert.Equal(1, MostSpecific(plain, heart));
    }

    [Fact]
    public void EqualRequirementsFallBackToSheetOrder() {
        Assert.Equal(1, MostSpecific(Rule(Collectibles.Heart, order: 5), Rule(Collectibles.Heart, order: 2)));
    }

    [Fact]
    public void RequirementsNeitherContainsFallBackToSheetOrder() {
        Assert.Equal(0, MostSpecific(Rule(Collectibles.Heart, order: 1), Rule(Collectibles.Cassette, order: 3)));
    }
}
