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
    // entering the start room, or the timer starting there
    Room,
    // once the summit launch into the start room is over (SegmentAutoDetect.AfterLaunchStarts)
    AfterLaunch,
}

internal enum EndKind {
    // entering the next start's room; with no next start, chapter time stopping
    // (the room map says which)
    NextStart,
    // chapter time stopping, whatever checkpoints come between (ILs)
    ChapterEnd,
    // the collect itself; with two collectibles, the later of the two
    Collect,
}

/// How a run of one imported row is detected. Written in game-checkpoint names,
/// never in rooms: rooms come from AreaData, which only the game thread reads.
internal sealed record SegmentRule(
    string Scope, string Chapter, string Name, string Anchor,
    StartKind Start, EndKind End,
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
    /// The index of the most specific rule: the one whose requirements contain
    /// every other's. On a tie, or when none contains them all, the first in
    /// sheet order.
    public static int MostSpecific(IReadOnlyList<SegmentRule> rules) {
        int best = -1;
        for (int i = 0; i < rules.Count; i++) {
            bool containsAll = true;
            for (int j = 0; j < rules.Count && containsAll; j++) {
                containsAll = rules[i].Contains(rules[j]);
            }

            if (containsAll && (best < 0 || rules[i].Order < rules[best].Order)) {
                best = i;
            }
        }

        if (best >= 0) {
            return best;
        }

        best = 0;
        for (int i = 1; i < rules.Count; i++) {
            if (rules[i].Order < rules[best].Order) {
                best = i;
            }
        }

        return best;
    }
}
