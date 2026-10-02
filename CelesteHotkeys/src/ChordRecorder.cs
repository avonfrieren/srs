using System;
using System.Collections.Generic;

namespace Celeste.Mod.CelesteHotkeys;

internal enum ChordStep {
    /// <summary>Still waiting for the chord to be let go.</summary>
    Recording,
    /// <summary>An input went down past <see cref="Bindable.MaxComboInputs"/> and was left out.</summary>
    Refused,
    /// <summary>The chord was let go; <see cref="ChordRecorder{T}.Inputs"/> is the binding to save.</summary>
    Done,
}

/// <summary>
///     Records one binding in one pass: every input pressed while recording joins the chord, and the
///     first one let go ends it. No MInput, no Engine — fed the held inputs once per frame.
/// </summary>
// The chord replaces the whole binding. Re-recording a smaller chord is how one input comes off.
internal sealed class ChordRecorder<T> where T : struct {
    /// <summary>How many frames a chord input must stay up before the release counts.</summary>
    // ⚠️ Frames, not the first frame up. A trigger is analog and read as a button past a threshold, and
    // one resting near it flickers up for a frame: ending on that frame saved LT + A as LT alone. A
    // real release lasts far longer than three frames.
    internal const int ReleaseFrames = 3;

    private readonly Func<T, bool> leading;
    private readonly List<T> chord = new();
    private readonly List<T> ignored = new();
    private int releasedFor;

    /// <param name="leading">
    ///     Inputs listed before the others whatever order they went down in — the modifiers, so the
    ///     binding reads "LeftControl G" even when both went down on one frame.
    /// </param>
    internal ChordRecorder(Func<T, bool> leading = null) => this.leading = leading;

    /// <summary>The chord so far, leading inputs first, then the rest in the order they went down.</summary>
    internal IReadOnlyList<T> Inputs => chord;

    /// <summary>Starts a chord. <paramref name="held"/> is what is held now, which never joins it.</summary>
    // ⚠️ What is held at the start is the press that opened the recording — Enter, or A on a pad —
    // and a Shift still down from it. An input is ignored until it has been let go once, and counts
    // when it goes down again.
    internal void Start(IEnumerable<T> held) {
        chord.Clear();
        ignored.Clear();
        ignored.AddRange(held);
        releasedFor = 0;
    }

    /// <summary>Advances one frame.</summary>
    /// <param name="held">Every input that may be recorded and is held this frame.</param>
    internal ChordStep Update(IReadOnlyList<T> held) {
        ignored.RemoveAll(input => !Contains(held, input));

        bool refused = false;
        for (int i = 0; i < held.Count; i++) {
            T input = held[i];
            if (ignored.Contains(input) || chord.Contains(input)) continue;
            if (chord.Count >= Bindable.MaxComboInputs) {
                // Ignored until let go, so one press is refused once and not every frame it is held.
                ignored.Add(input);
                refused = true;
                continue;
            }
            Add(input);
        }

        bool released = false;
        for (int i = 0; i < chord.Count; i++) {
            if (Contains(held, chord[i])) continue;
            released = true;
            break;
        }
        releasedFor = released ? releasedFor + 1 : 0;

        if (releasedFor >= ReleaseFrames) return ChordStep.Done;
        return refused ? ChordStep.Refused : ChordStep.Recording;
    }

    private void Add(T input) {
        if (leading is null || !leading(input)) {
            chord.Add(input);
            return;
        }
        int at = 0;
        while (at < chord.Count && leading(chord[at])) at++;
        chord.Insert(at, input);
    }

    private static bool Contains(IReadOnlyList<T> inputs, T input) {
        for (int i = 0; i < inputs.Count; i++) {
            if (EqualityComparer<T>.Default.Equals(inputs[i], input)) return true;
        }
        return false;
    }
}
