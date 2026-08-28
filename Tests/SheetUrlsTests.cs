using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// The three tab urls are stored settings, so new defaults reach nobody who has
// ever saved. Migrate is what actually moves a player off the workbook frozen
// on 2026-08-28 — which still answers, so the failure it prevents is silent:
// srs keeps importing, from a document that no longer moves.
public class SheetUrlsTests {
    private const string Frozen = "18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y";

    [Fact]
    public void RepointsAStoredUrlAndKeepsItsGid() {
        string migrated = SheetUrls.Migrate(
            $"https://docs.google.com/spreadsheets/d/{Frozen}/edit?gid=1885706573");

        Assert.Equal(
            $"https://docs.google.com/spreadsheets/d/{SheetUrls.ReferenceId}/edit?gid=1885706573",
            migrated);
    }

    // the reference is a Drive copy of the frozen workbook, so it kept its
    // sheetIds: substituting the id alone leaves a tab a player picked on
    // purpose pointing at the same tab
    [Fact]
    public void KeepsAGidThePlayerChanged() {
        string migrated = SheetUrls.Migrate(
            $"https://docs.google.com/spreadsheets/d/{Frozen}/edit?gid=42");

        Assert.EndsWith("gid=42", migrated);
    }

    // null means "nothing to save", so anything already current or deliberate
    // has to come back null rather than unchanged
    [Fact]
    public void LeavesTheCurrentUrlAlone() {
        Assert.Null(SheetUrls.Migrate(SheetUrls.EditUrlPrefix + "1796170425"));
    }

    [Fact]
    public void LeavesACustomWorkbookAlone() {
        Assert.Null(SheetUrls.Migrate(
            "https://docs.google.com/spreadsheets/d/1someoneElsesOwnCopyOfTheSheet/edit?gid=0"));
    }

    [Fact]
    public void LeavesNothingToMigrateAlone() {
        Assert.Null(SheetUrls.Migrate(null));
        Assert.Null(SheetUrls.Migrate(""));
    }

    // the id is matched as a whole token: a workbook whose id merely starts
    // with the frozen one is a different document
    [Fact]
    public void DoesNotMatchTheFrozenIdAsAPrefix() {
        Assert.Null(SheetUrls.Migrate(
            $"https://docs.google.com/spreadsheets/d/{Frozen}XY/edit?gid=0"));
    }

    [Fact]
    public void IsIdempotent() {
        string once = SheetUrls.Migrate($"https://docs.google.com/spreadsheets/d/{Frozen}/edit?gid=0");

        Assert.NotNull(once);
        Assert.Null(SheetUrls.Migrate(once));
    }
}
