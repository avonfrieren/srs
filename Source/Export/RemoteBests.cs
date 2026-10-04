using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

namespace Celeste.Mod.SpeedrunSheet;

public enum RemoteState { NotLoaded, Loading, Ready, Error }

/// Cache of what the sheet holds. Deliberately NOT registered with
/// SpeedrunTool.SaveLoad: a savestate load must not wipe downloaded data.
public static class RemoteBests {
    /// Everything a reader needs, published in one assignment. Written by the
    /// worker resolving a fetch, read every frame by the game thread: separate
    /// fields could be seen out of order, Ready before its index, and the rows
    /// would then compare against values that have not arrived.
    /// AcceptedAt is on the monotonic clock: it is only ever asked how old the
    /// data is, and the wall clock can jump.
    private sealed record Snapshot(
        RemoteState State, Dictionary<(string, string, string), RemoteRow> Index, string Error, long AcceptedAt);

    private static volatile Snapshot current = Empty(RemoteState.NotLoaded);

    public static RemoteState State => current.State;
    public static string Error => current.Error;

    /// How long ago the held answer arrived, and TimeSpan.MaxValue when there
    /// is none. Read by the screen to decide whether asking again would say
    /// anything new.
    public static TimeSpan Age =>
        current is { State: RemoteState.Ready } ready
            ? TimeSpan.FromMilliseconds(Environment.TickCount64 - ready.AcceptedAt)
            : TimeSpan.MaxValue;

    // Export must not be submittable while a row's remote comparison is still a
    // guess (Loading/NotLoaded) or known-stale (Error)
    public static bool IsResolved => State == RemoteState.Ready;

    public static void Reset() => current = Empty(RemoteState.NotLoaded);

    /// Drops the times held: the sheet may have moved, and did if this screen
    /// wrote to it. The screen waits for the answer before building its table,
    /// so nothing is shown against the emptied index.
    public static void BeginFetch() => current = Empty(RemoteState.Loading);

    public static void Accept(IEnumerable<RemoteRow> rows) {
        Dictionary<(string, string, string), RemoteRow> built = [];
        foreach (RemoteRow row in rows) {
            built[Key(row.Tab, row.Chapter, row.Cp)] = row;
        }

        current = new Snapshot(RemoteState.Ready, built, null, Environment.TickCount64);
    }

    /// Keeps the index held, so a compare-and-swap rather than a plain write: an
    /// Accept landing between the read and the write would otherwise lose its
    /// rows to the older index this copied.
    public static void Fail(string error) {
        Snapshot seen;
        do {
            seen = current;
        } while (Interlocked.CompareExchange(ref current, seen with { State = RemoteState.Error, Error = error }, seen)
                 != seen);
    }

    public static bool TryGet(SheetRowRef row, out RemoteRow value) =>
        current.Index.TryGetValue(Key(row.Tab, row.Chapter, row.Cp), out value);

    private static Snapshot Empty(RemoteState state) => new(state, [], null, 0);

    private static (string, string, string) Key(string tab, string chapter, string cp) =>
        (Normalize(tab), Normalize(chapter), Normalize(cp));

    /// Same rule as the Apps Script's norm(): NFC, collapsed whitespace, and the
    /// U+FE0F variation selector dropped. Emoji are NOT stripped: they are what
    /// tells "7a Start" from "7a Start \U0001F48E".
    private static string Normalize(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }

        string withoutSelectors = value.Replace("\uFE0F", "");
        string collapsed = Regex.Replace(withoutSelectors.Normalize(NormalizationForm.FormC), @"\s+", " ");
        return collapsed.Trim().ToLowerInvariant();
    }
}
