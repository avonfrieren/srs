using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Celeste.Mod.SpeedrunSheet;

public enum RemoteState { NotLoaded, Loading, Ready, Error }

/// Cache of what the sheet currently holds, filled by the export screen and read
/// by it alone so far. Deliberately NOT registered with SpeedrunTool.SaveLoad:
/// loading a savestate must not wipe downloaded data.
public static class RemoteBests {
    // written by the worker thread that resolves a fetch, read every frame by
    // the game thread. Never mutated in place: Accept builds a fresh dictionary
    // and publishes it with one assignment, so a read landing mid-fetch sees
    // either the whole previous index or the whole new one, and never a
    // dictionary being cleared and refilled under it
    private static volatile Dictionary<(string, string, string), RemoteRow> index = new();

    public static RemoteState State { get; private set; } = RemoteState.NotLoaded;

    // true once the fetch has actually resolved with data: Export must not be
    // submittable while every row's remote comparison is still a guess
    // (Loading/NotLoaded) or known-stale (Error)
    public static bool IsResolved => State == RemoteState.Ready;
    public static string Error { get; private set; }
    public static IReadOnlyCollection<RemoteRow> Rows => index.Values;

    public static void Reset() {
        index = new();
        State = RemoteState.NotLoaded;
        Error = null;
    }

    /// Drops what is held: a fetch is starting because the sheet may have moved,
    /// and it moved at least once already if this screen wrote to it. Keeping
    /// the old rows would be worse than holding none, since a row built against
    /// them keeps them for the whole session (see ExportMenu.RefreshRemote).
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
