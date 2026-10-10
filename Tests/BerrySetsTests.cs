using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

// a berry set against 1A's red berries as the map files them
public class BerrySetsTests {
    private static readonly MapBerry[] ForsakenCity = [
        new("2", 11, 0), new("3", 9, 0), new("3b", 2, 0), new("5z", 10, 0), new("5", 21, 0), new("5a", 2, 0),
        new("7zb", 2, 1), new("6", 12, 1), new("s1", 9, 1), new("7z", 3, 1), new("8zb", 1, 1), new("7a", 12, 1),
        new("9z", 3, 1), new("8b", 1, 1), new("9", 14, 1),
        new("10zb", 1, 2), new("11", 9, 2), new("9b", 9, 2), new("9c", 2, 2), new("12z", 8, 2),
    ];

    private static int CheckpointOf(string name) => name switch {
        "Start" => 0,
        "Crossing" => 1,
        "Chasm" => 2,
        _ => -1,
    };

    private static List<string> Keys(BerrySet set) =>
        BerrySets.Resolve(set, ForsakenCity, CheckpointOf)?.Order().ToList();

    private static BerrySet Row(string chapter, string name) => RowTraits.All[(chapter, name)].Berries;

    private static List<string> Sorted(params string[] keys) => keys.Order().ToList();

    [Fact]
    public void ACheckpointsBerriesAreItsOwn() {
        Assert.Equal(Sorted("2:11", "3:9", "3b:2", "5z:10", "5:21", "5a:2"), Keys(new BerrySet("Start")));
    }

    [Fact]
    public void TheChapterIsEveryBerry() {
        Assert.Equal(20, BerrySets.Resolve(BerrySet.WholeChapter, ForsakenCity, CheckpointOf).Count);
    }

    // 1A's Start to Heart, as the table holds it
    [Fact]
    public void RoomsAddToACheckpoint() {
        Assert.Equal(Sorted("2:11", "3:9", "3b:2", "5z:10", "5:21", "5a:2", "6:12", "s1:9"),
            Keys(Row("1a", "ARB Start to Heart")));
    }

    // 1A's Crossing to Heart
    [Fact]
    public void RoomsAloneAreASet() {
        Assert.Equal(Sorted("6:12", "s1:9"), Keys(Row("1a", "ARB Crossing to Heart")));
    }

    // 1A's Crossing and Chasm
    [Fact]
    public void ExceptLeavesRoomsOut() {
        Assert.Equal(Sorted("6:12", "s1:9", "7z:3", "8zb:1", "7a:12", "9z:3", "8b:1", "9:14"),
            Keys(Row("1a", "ARB Crossing")));
        Assert.Equal(Sorted("9b:9", "9c:2", "12z:8"), Keys(Row("1a", "ARB Chasm")));
        // 4A's, which no map here holds
        Assert.Equal(("Cliff Face", "d-00b"),
            (Row("4a", "ARB Cliff Face (from RTM)").Checkpoint, Row("4a", "ARB Cliff Face (from RTM)").Except.Single()));
    }

    // a set read wrong must never pass for a set met
    [Fact]
    public void ASetTheMapDoesNotHoldIsNull() {
        Assert.Null(Keys(new BerrySet("Chasn")));
        Assert.Null(Keys(new BerrySet(Rooms: ["6", "s9"])));
        // 7zb is not a Chasm room: the exception removes nothing
        Assert.Null(Keys(new BerrySet("Chasm", Except: ["7zb"])));
        Assert.Null(Keys(new BerrySet()));
        Assert.Null(BerrySets.Resolve(BerrySet.WholeChapter, [], CheckpointOf));
    }

    // Farewell's moon berry is a set of its own, and no red set holds one
    [Fact]
    public void TheMoonBerryIsNoRedBerry() {
        MapBerry[] map = [new("a-01", 4, 0), new("j-19", 7, 0, Moon: true)];

        Assert.Equal(["j-19:7"], BerrySets.Resolve(BerrySet.MoonBerry, map, CheckpointOf));
        Assert.Equal(["a-01:4"], BerrySets.Resolve(BerrySet.WholeChapter, map, CheckpointOf));
        Assert.Equal(["a-01:4"], BerrySets.Resolve(new BerrySet("Start"), map, CheckpointOf));
        Assert.Null(BerrySets.Resolve(BerrySet.MoonBerry, ForsakenCity, CheckpointOf));
    }
}
