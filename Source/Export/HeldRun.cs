using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

/// The run SessionBests holds, kept game-free so the tests can pin what a
/// second run replaces.
internal sealed class HeldRun {
    /// Scope is the side played ("6a" and "6b" share the chapter "6a/b");
    /// Chapter and Name are the sheet segment that was actually run.
    internal readonly record struct Run(string Scope, string Chapter, string Name, long Ticks);

    public Run? Current { get; private set; }

    /// The best of one segment, never of two: Hollows Tape stops at the
    /// cassette and Hollows at the next checkpoint, so the smaller of the two
    /// is a time of neither. A run of another segment replaces the held one.
    /// Returns whether the held run changed.
    public bool Offer(string scope, string chapter, string name, long ticks) {
        if (ticks <= 0) {
            return false;
        }

        if (Current is { } held && held.Scope == scope && held.Chapter == chapter && held.Name == name
            && ticks >= held.Ticks) {
            return false;
        }

        Current = new Run(scope, chapter, name, ticks);
        return true;
    }

    public void Clear() => Current = null;

    /// The segments a held run can be moved onto, in sheet order, itself
    /// included: same chapter, same game checkpoint, same end. Auto-detect
    /// cannot tell those apart (Huge Mess run on the heart route while the
    /// category was Any%), and nothing else is the same run.
    ///
    /// ⚠️ Never the whole chapter: on 7a that is seven checkpoints, and two
    /// arrow presses write 0m's time into 3000m's row. Never another end
    /// either: a Hollows time is not a Hollows Tape time.
    public static List<SheetSegment> CandidatesAmong(IEnumerable<SheetSegment> segments, SheetSegment segment,
        string scope) {
        List<SheetSegment> candidates = [];
        string anchor = segment == null || scope == null ? null : SegmentAutoDetect.GameNameOf(scope, segment.Name);
        if (anchor == null) {
            // no game checkpoint behind this row: nothing can be said about
            // what shares its start room, so offer nothing rather than a chapter
            return candidates;
        }

        foreach (SheetSegment other in segments) {
            if (other.Chapter == segment.Chapter
                && other.End == segment.End
                && SheetLabels.TryMap(other.Chapter, other.Name, out _)
                && SegmentAutoDetect.GameNameOf(scope, other.Name) == anchor) {
                candidates.Add(other);
            }
        }

        return candidates;
    }
}
