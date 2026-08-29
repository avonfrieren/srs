using System;
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

    // the route the player records on their own sheet, per category. Published
    // the same way and for the same reason as the index above
    private static volatile Dictionary<string, string> routes = new();

    // the chapter totals each category tab's summary block shows, keyed by
    // (category, chapter). Published the same way again
    private static volatile Dictionary<(string, string), string> chapterSobs = new();

    public static RemoteState State { get; private set; } = RemoteState.NotLoaded;

    // true once the fetch has actually resolved with data: Export must not be
    // submittable while every row's remote comparison is still a guess
    // (Loading/NotLoaded) or known-stale (Error)
    public static bool IsResolved => State == RemoteState.Ready;
    public static string Error { get; private set; }
    public static IReadOnlyCollection<RemoteRow> Rows => index.Values;

    public static void Reset() {
        index = new();
        routes = new();
        chapterSobs = new();
        State = RemoteState.NotLoaded;
        Error = null;
    }

    /// Drops the times held: a fetch is starting because the sheet may have moved,
    /// and it moved at least once already if this screen wrote to it. Nothing is
    /// shown against the emptied index -- the screen waits for the answer before
    /// building its table -- so this leaves no window in which stale values are
    /// on display.
    public static void BeginFetch() {
        index = new();
        // the routes are deliberately kept: which route a player runs is not a
        // time that can go stale mid-session, and dropping it made every open
        // start on the wrong route and collect its rows a second time
        State = RemoteState.Loading;
        Error = null;
    }

    public static void Accept(IEnumerable<RemoteRow> rows, IEnumerable<RemoteRoute> known = null,
        IEnumerable<RemoteSob> totals = null) {
        Dictionary<(string, string, string), RemoteRow> built = [];
        foreach (RemoteRow row in rows) {
            built[Key(row.Tab, row.Chapter, row.Cp)] = row;
        }

        Dictionary<string, string> chosen = new(StringComparer.OrdinalIgnoreCase);
        foreach (RemoteRoute route in known ?? []) {
            if (!string.IsNullOrWhiteSpace(route?.Category) && !string.IsNullOrWhiteSpace(route.Route)) {
                chosen[route.Category.Trim()] = route.Route.Trim();
            }
        }

        // keyed on the route the block was computed for, not merely the
        // category: the summary is one route's, and a view on another must not
        // read it (Sob checks the route it asks for)
        Dictionary<(string, string), string> summed = [];
        foreach (RemoteSob total in totals ?? []) {
            foreach (RemoteChapterSob chapter in total?.Chapters ?? []) {
                if (!string.IsNullOrWhiteSpace(chapter?.Segment)) {
                    summed[(Normalize(total.Category) + "|" + Normalize(total.Route),
                        Normalize(chapter.Segment))] = chapter.Time;
                }
            }
        }

        chapterSobs = summed;
        routes = chosen;
        index = built;
        State = RemoteState.Ready;
        Error = null;
    }

    public static void Fail(string error) {
        State = RemoteState.Error;
        Error = error;
    }

    /// the route the player's own sheet says they run in that category, or
    /// null: an older script does not send this, and neither does a sheet whose
    /// Home Page has no route in that column
    public static string RouteFor(string category) =>
        category != null && routes.TryGetValue(category, out string route) ? route : null;

    /// The chapter's sum of best as the player's own sheet shows it, or null.
    ///
    /// The route is part of the question rather than a filter applied after:
    /// the summary block is computed for the one route the tab is set to, so
    /// asking under another route has no answer rather than a wrong one.
    public static string Sob(string category, string route, string chapter) =>
        category != null && route != null && chapter != null
        && chapterSobs.TryGetValue((Normalize(category) + "|" + Normalize(route), Normalize(chapter)),
            out string time)
            ? time
            : null;

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
