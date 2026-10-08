using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// Parse takes whatever tabs the cache or the download holds
public class ParseTabsTests {
    [Fact]
    public void ATabLeftOutIsSkipped() {
        SheetData data = SheetData.Parse(new Dictionary<StandardsTab, string> {
            [StandardsTab.BSides] = Fixtures.BSides,
        });

        Assert.NotNull(data.CheckpointBlock.Find("5a/b", "Central Chamber"));
        Assert.Null(data.CheckpointBlock.Find("1a", "Start"));
    }

    // the tabs merge in the table's order whatever the dictionary's
    [Fact]
    public void TheTabsMergeInTheTablesOrder() {
        SheetData data = SheetData.Parse(new Dictionary<StandardsTab, string>(Fixtures.ByTab.Reverse()));

        Assert.Equal(
            Fixtures.Parsed.CheckpointBlock.Segments.ConvertAll(s => (s.Chapter, s.Name)),
            data.CheckpointBlock.Segments.ConvertAll(s => (s.Chapter, s.Name)));
    }
}
