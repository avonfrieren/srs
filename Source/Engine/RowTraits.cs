using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// What a row's label does not say, by srs's (chapter, name): a start from
/// the checkpoint's map spawn, a heart that ends the row or is only required,
/// an entry room of its own, a berry set that is not its checkpoint's.
internal readonly record struct RowTraits(
    bool MapSpawn = false, bool EndsOnHeart = false, Collectibles Requires = Collectibles.None,
    string EntryRoom = null, BerrySet Berries = null) {
    // rooms as the game names them. A key no row carries never applies
    // (SheetConsistencyTests checks each one)
    internal static readonly Dictionary<(string Chapter, string Name), RowTraits> All = new() {
        [("1a", "ARB Start to Heart")] = new(EndsOnHeart: true, Berries: new BerrySet("Start", Rooms: ["6", "s1"])),
        [("1a", "ARB Crossing to Heart")] = new(MapSpawn: true, EndsOnHeart: true, Berries: new BerrySet(Rooms: ["6", "s1"])),
        // 7zb, and 11 and 10zb, belong to the rows that return to the map
        [("1a", "ARB Crossing")] = new(MapSpawn: true, Requires: Collectibles.Heart, Berries: new BerrySet("Crossing", Except: ["7zb"])),
        [("1a", "ARB Chasm")] = new(MapSpawn: true, Berries: new BerrySet("Chasm", Except: ["11", "10zb"])),
        [("3a", "ARB Huge Mess")] = new(Requires: Collectibles.Heart),
        [("3a", "ARB IL")] = new(Requires: Collectibles.Heart),
        [("4a", "ARB Shrine")] = new(Requires: Collectibles.Heart),
        // d-00b is taken on the way out of Old Trail on that route
        [("4a", "ARB Cliff Face (from RTM)")] = new(MapSpawn: true, Berries: new BerrySet("Cliff Face", Except: ["d-00b"])),
        // d-00 is entered from c-08 on the A-side, and from the berry room here
        [("4a", "ARB Cliff Face")] = new(EntryRoom: "c-10"),
        [("4a", "ARB IL")] = new(Requires: Collectibles.Heart),
        [("5a", "ARB Depths")] = new(MapSpawn: true),
        // 3000m's pieces are no checkpoint of the map: their berries by room
        [("7a", "ARB Downdraft")] = new(Berries: new BerrySet(Rooms: ["g-00b"])),
        [("7a", "ARB Updraft")] = new(Berries: new BerrySet(Rooms: ["g-01"])),
        [("7a", "ARB Nodraft")] = new(Berries: new BerrySet(Rooms: ["g-03"])),
    };

    public static RowTraits Of(string chapter, string name) => All.GetValueOrDefault((chapter, name));
}
