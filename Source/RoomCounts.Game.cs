using Celeste.Mod.SpeedrunTool;

namespace Celeste.Mod.SpeedrunSheet;

// the half of RoomCounts that talks to SpeedrunTool, split from the table
// itself so the tests can compile the table without the game references
public static partial class RoomCounts {
    public static void Apply(SheetSegment segment) {
        if (segment != null && SpeedrunToolSettings.Instance is { } settings) {
            settings.NumberOfRooms = TargetFor(segment);
        }
    }
}
