using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// what the HUD and the export show when several rows close on one frame
public class SpecificityTests {
    private static SegmentRule Rule(Collectibles requires, bool berries = false, int order = 0) =>
        new("3a", "3a", $"r{order}", "Huge Mess", StartKind.Room, StartSetup.NextRoom, EndKind.NextStart,
            Collectibles.None, requires, berries, null, 0, 0, order);

    [Fact]
    public void TheRowWhoseRequirementsContainTheOthersWins() {
        SegmentRule plain = Rule(Collectibles.None, order: 0);
        SegmentRule heart = Rule(Collectibles.Heart, order: 1);
        SegmentRule arb = Rule(Collectibles.Heart, berries: true, order: 2);

        Assert.Equal(2, Specificity.MostSpecific([plain, heart, arb]));
        Assert.Equal(1, Specificity.MostSpecific([plain, heart]));
    }

    [Fact]
    public void EqualRequirementsFallBackToSheetOrder() {
        Assert.Equal(1, Specificity.MostSpecific([Rule(Collectibles.Heart, order: 5), Rule(Collectibles.Heart, order: 2)]));
    }

    [Fact]
    public void RequirementsNeitherContainsFallBackToSheetOrder() {
        Assert.Equal(0, Specificity.MostSpecific([Rule(Collectibles.Heart, order: 1), Rule(Collectibles.Cassette, order: 3)]));
    }
}
