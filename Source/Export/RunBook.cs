using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// The session's best of every row, game-free so the tests can pin what a run
/// replaces. A row's best is replaced only by a faster run of that same row.
internal sealed class RunBook {
    /// Scope is the side played ("6a" and "6b" share the chapter "6a/b").
    internal readonly record struct Run(string Scope, string Chapter, string Name, long Ticks);

    private readonly Dictionary<(string Chapter, string Name), Run> best = [];

    public IReadOnlyCollection<Run> All => best.Values;

    /// Offers what one event closed; returns the runs that improved.
    public List<Run> Offer(IReadOnlyList<SegmentRecord> records) {
        List<Run> improved = [];
        foreach (SegmentRecord record in records) {
            SegmentRule rule = record.Rule;
            if (record.Ticks <= 0
                || (best.TryGetValue((rule.Chapter, rule.Name), out Run held) && record.Ticks >= held.Ticks)) {
                continue;
            }

            Run run = new(rule.Scope, rule.Chapter, rule.Name, record.Ticks);
            best[(rule.Chapter, rule.Name)] = run;
            improved.Add(run);
        }

        return improved;
    }
}
