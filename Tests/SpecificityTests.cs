using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// what the HUD and the export show when several rows close on one frame
public class SpecificityTests {
    private static SegmentRule Rule(Collectibles requires, bool berries = false, int order = 0,
        bool chapterRun = false) =>
        new("3a", $"r{order}", "Huge Mess", StartKind.Room, StartSetup.NextRoom, EndKind.NextStart,
            Collectibles.None, requires, null, 0, 0, order) {
            ChapterRun = chapterRun, Berries = berries ? BerrySet.WholeChapter : null,
        };

    private static int MostSpecific(params SegmentRule[] rules) => Specificity.MostSpecific(rules, chapterRun: false);

    // a chapter run and the segment ending with it are picked apart, so both are shown
    [Fact]
    public void ChapterRunsAndSegmentsAreChosenApart() {
        SegmentRule last = Rule(Collectibles.None, order: 0);
        SegmentRule lastWithTape = Rule(Collectibles.Cassette, order: 1);
        SegmentRule il = Rule(Collectibles.None, order: 2, chapterRun: true);
        SegmentRule ilWithHeart = Rule(Collectibles.Heart, order: 3, chapterRun: true);
        SegmentRule[] closed = [last, lastWithTape, il, ilWithHeart];

        Assert.Equal(1, Specificity.MostSpecific(closed, chapterRun: false));
        Assert.Equal(3, Specificity.MostSpecific(closed, chapterRun: true));
        Assert.Equal(-1, Specificity.MostSpecific([last], chapterRun: true));
        Assert.Equal(-1, Specificity.MostSpecific([il], chapterRun: false));
    }

    // 7A's 1500m with the tape and the berries but no gem: the plain row asks
    // for less than either, so it is not the one shown
    [Fact]
    public void WhenNoRowContainsTheOthersARowNothingOutdoesWins() {
        SegmentRule plain = Rule(Collectibles.None, order: 0);
        SegmentRule tape = Rule(Collectibles.Cassette, order: 1);
        SegmentRule arb = Rule(Collectibles.None, berries: true, order: 2);

        Assert.Equal(1, MostSpecific(plain, tape, arb));
        Assert.Equal(1, MostSpecific(arb, tape, plain));
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
