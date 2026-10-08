using System;
using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// What a run must collect during a segment, or what ends it.
[Flags]
internal enum Collectibles {
    None = 0,
    Heart = 1,
    Cassette = 2,
    Gem = 4,
}

internal enum StartKind {
    // entering the start room, or a restart in it (RunTracker.Restart)
    Room,
    // once the launch (the intro jump) into the start room is over (SegmentAutoDetect.AfterLaunchStarts)
    AfterLaunch,
}

internal enum StartSetup {
    // valid only when the first room is entered from the checkpoint before
    NextRoom,
    // valid from its start point: in a chain, or from a savestate at the start room's spawn
    CurrentRoom,
}

internal enum EndKind {
    // entering the next start's room; with no next start, chapter time stopping
    // (the room map says which)
    NextStart,
    // chapter time stopping, whatever checkpoints come between: a whole chapter
    ChapterEnd,
    // the collect itself; with two collectibles, the later of the two
    Collect,
    // Restart Chapter, on the old session's last reading ("RC" rows)
    Restart,
}

/// How a run of one imported row is detected. Written in game-checkpoint names,
/// never in rooms: rooms come from AreaData, which only the game thread reads.
internal sealed record SegmentRule(
    string Scope, string Chapter, string Name, string Anchor,
    StartKind Start, StartSetup Setup, EndKind End,
    Collectibles EndsOn, Collectibles Requires, bool RequiresBerries,
    // 1 or 2 for Farewell's DTS twins, read at the end; null = no dash rule
    int? Dashes,
    long HeadTicks, long TailTicks,
    // position in the sheet, the last tie-breaker
    int Order) {
    /// Whether this rule asks for everything the other asks for.
    public bool Contains(SegmentRule other) =>
        (Requires & other.Requires) == other.Requires && (RequiresBerries || !other.RequiresBerries);
}

internal static class Specificity {
    /// The index of the most specific rule among the whole chapters, or among
    /// the segments: the one whose requirements contain every other's; on a
    /// tie, or when none contains them all, the first in sheet order. -1 when
    /// there is none of that kind.
    public static int MostSpecific(IReadOnlyList<SegmentRule> rules, bool wholeChapter) {
        int best = -1;
        int first = -1;
        for (int i = 0; i < rules.Count; i++) {
            if (rules[i].End == EndKind.ChapterEnd != wholeChapter) {
                continue;
            }

            if (first < 0 || rules[i].Order < rules[first].Order) {
                first = i;
            }

            bool containsAll = true;
            for (int j = 0; j < rules.Count && containsAll; j++) {
                containsAll = rules[j].End == EndKind.ChapterEnd != wholeChapter || rules[i].Contains(rules[j]);
            }

            if (containsAll && (best < 0 || rules[i].Order < rules[best].Order)) {
                best = i;
            }
        }

        return best >= 0 ? best : first;
    }
}
