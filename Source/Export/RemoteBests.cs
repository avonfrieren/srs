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
    // written by the worker resolving a fetch, read every frame by the game
    // thread. Never mutated in place: Accept publishes a fresh dictionary in one
    // assignment, so a read sees one whole index or the other
    private static volatile Dictionary<(string, string, string), RemoteRow> index = new();


    public static RemoteState State { get; private set; } = RemoteState.NotLoaded;

    // when Accept last took an answer in, on the monotonic clock: this is only
    // ever asked how old the data is, and the wall clock can jump
    private static long acceptedAt;

    /// How long ago the held answer arrived, and TimeSpan.MaxValue when there
    /// is none. Read by the screen to decide whether asking again would say
    /// anything new.
    public static TimeSpan Age =>
        State == RemoteState.Ready
            ? TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref acceptedAt))
            : TimeSpan.MaxValue;

    // Export must not be submittable while a row's remote comparison is still a
    // guess (Loading/NotLoaded) or known-stale (Error)
    public static bool IsResolved => State == RemoteState.Ready;
    public static string Error { get; private set; }

    public static void Reset() {
        index = new();
        State = RemoteState.NotLoaded;
        Error = null;
    }

    /// Drops the times held: the sheet may have moved, and did if this screen
    /// wrote to it. The screen waits for the answer before building its table,
    /// so nothing is shown against the emptied index.
    public static void BeginFetch() {
        index = new();
        State = RemoteState.Loading;
        Error = null;
    }

    public static void Accept(IEnumerable<RemoteRow> rows) {
        Dictionary<(string, string, string), RemoteRow> built = [];
        foreach (RemoteRow row in rows) {
            built[Key(row.Tab, row.Chapter, row.Cp)] = row;
        }

        index = built;
        Interlocked.Exchange(ref acceptedAt, Environment.TickCount64);
        State = RemoteState.Ready;
        Error = null;
    }

    public static void Fail(string error) {
        State = RemoteState.Error;
        Error = error;
    }

    public static bool TryGet(SheetRowRef row, out RemoteRow value) =>
        index.TryGetValue(Key(row.Tab, row.Chapter, row.Cp), out value);

    private static (string, string, string) Key(string tab, string chapter, string cp) =>
        (Normalize(tab), Normalize(chapter), Normalize(cp));

    /// Same rule as the Apps Script's norm(): NFC, collapsed whitespace, and the
    /// U+FE0F variation selector dropped. Emoji are NOT stripped — they are what
    /// tells "0m" from "0m \U0001F48E", and ten such pairs exist in the sheet.
    private static string Normalize(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }

        string withoutSelectors = value.Replace("\uFE0F", "");
        string collapsed = Regex.Replace(withoutSelectors.Normalize(NormalizationForm.FormC), @"\s+", " ");
        return collapsed.Trim().ToLowerInvariant();
    }
}
