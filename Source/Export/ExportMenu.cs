using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SpeedrunTool.Message;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

/// One checkbox row of the export screen. TextMenu has no built-in item for this.
internal sealed class UpdateRow : TextMenu.Item {
    // LiveSplit's default ahead/behind colours
    private static readonly Color Ahead = Calc.HexToColor("00CC36");
    private static readonly Color Behind = Calc.HexToColor("CC1200");

    private readonly ExportColumns columns;
    private readonly bool odd;
    // the list Submit reads. Rows read through it rather than holding their own
    // copy, so what is shown and what is written cannot drift apart
    private readonly List<PendingUpdate> slot;
    private readonly int index;

    public PendingUpdate Update => slot[index];

    public UpdateRow(List<PendingUpdate> slot, int index, ExportColumns columns, bool odd) {
        this.slot = slot;
        this.index = index;
        this.columns = columns;
        this.odd = odd;
        // base TextMenu.Item defaults this to false; without it the cursor can
        // never land on the row and ConfirmPressed() is never dispatched. Rows
        // the session holds no run for are the sheet shown back: there is
        // nothing to tick, so the cursor skips them
        Selectable = slot[index].HasLocal;
    }

    public override void ConfirmPressed() {
        if (Update.HasLocal) {
            Update.Selected = !Update.Selected;
        }
    }

    public override float LeftWidth() => columns.TotalWidth;
    public override float Height() => ExportColumns.RowHeight;

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        PendingUpdate update = Update;

        // both parities are banded, at two alphas: one stripe over bare
        // background reads as a tinted list, two read as a grid
        Color band = highlighted
            ? Color.White * (0.22f * alpha)
            : Color.White * ((odd ? 0.09f : 0.04f) * alpha);
        ExportColumns.Band(position, Container.Width, band);

        if (update.HasLocal) {
            ExportColumns.Checkbox(position, update.Selected, Color.White * alpha);
        }

        Color text = (update.HasLocal ? Color.White : Color.Gray) * alpha;
        ExportColumns.Text(update.Label, position, columns.LabelX, text, alpha, left: true);
        ExportColumns.Text(update.RemoteText, position, columns.RemoteX, Color.Gray * alpha, alpha);
        ExportColumns.Text(update.LocalText, position, columns.LocalX, text, alpha);
        ExportColumns.Text(update.DeltaText, position, columns.DeltaX, DeltaColor(update) * alpha, alpha);
    }

    // neutral when there is nothing to compare against, and when the two times
    // are equal: "+0.000" in red says a regression that did not happen. An
    // unreadable cell lands here too, through RemoteTicks staying null, and
    // grey is right for it: its "?" is not a regression, it is a refusal
    private static Color DeltaColor(PendingUpdate update) {
        if (update.RemoteTicks == null || update.LocalTicks == update.RemoteTicks.Value) {
            return Color.Gray;
        }

        return update.LocalTicks < update.RemoteTicks.Value ? Ahead : Behind;
    }
}

/// A line of the second header: what the table is a view of. Drawn at the
/// table's scale rather than as a menu Option, which reads as a setting --
/// this is the table's chrome, and nothing chosen here is written to
/// SrsSettings.
internal sealed class ViewControls : TextMenu.Item {
    private readonly ExportColumns columns;
    private readonly Func<string> label;
    private readonly Action<int> cycle;

    public ViewControls(ExportColumns columns, Func<string> label, Action<int> cycle) {
        this.columns = columns;
        this.label = label;
        this.cycle = cycle;
        Selectable = true;
    }

    public override void LeftPressed() => Cycle(-1);
    public override void RightPressed() => Cycle(1);

    private void Cycle(int direction) {
        cycle(direction);
        Audio.Play(direction < 0 ? "event:/ui/main/rollover_up" : "event:/ui/main/rollover_down");
    }

    public override float LeftWidth() => columns.TotalWidth;
    public override float Height() => ExportColumns.RowHeight;

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        Color color = (highlighted ? Color.White : Color.Gray) * alpha;
        string text = label();

        float x = (Container.Width - ExportColumns.TextWidth(text)) / 2f;
        ExportColumns.Text(text, position, x, color, alpha, left: true);
        if (highlighted) {
            ExportColumns.ArrowsAt(position, x, text, color, alpha);
        }
    }
}

internal sealed class GroupRow(ExportColumns columns, string label) : TextMenu.Item {
    public override float LeftWidth() => columns.TotalWidth;
    public override float Height() => ExportColumns.RowHeight;

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        Color color = Color.Gray * alpha;

        ExportColumns.Rule(position, Container.Width, -Height() / 2f, alpha);
        ExportColumns.Rule(position, Container.Width, Height() / 2f, alpha);

        // the chapter the rows below belong to: with one row on screen it is
        // the only place it appears, the row labels having dropped it
        ExportColumns.Text(label, position, columns.LabelX, color, alpha, left: true);
        ExportColumns.Text(Dialog.Clean("SRS_EXPORT_COL_SHEET"), position, columns.RemoteX, color, alpha);
        ExportColumns.Text(Dialog.Clean("SRS_EXPORT_COL_LOCAL"), position, columns.LocalX, color, alpha);
        ExportColumns.Text(Dialog.Clean("SRS_EXPORT_COL_DELTA"), position, columns.DeltaX, color, alpha);
    }
}

/// A rule closing the table, so the buttons below read as buttons and not as
/// two more rows. OuiJournalPage draws its section separators the same way.
internal sealed class TableFooter(ExportColumns columns) : TextMenu.Item {
    public override float LeftWidth() => columns.TotalWidth;
    public override float Height() => ExportColumns.RowHeight / 2f;

    public override void Render(Vector2 position, bool highlighted) =>
        ExportColumns.Rule(position, Container.Width, 0f, Container.Alpha);
}

/// Column geometry and the primitives every row of the table draws with.
/// TextMenu hands an item the vertical CENTRE of its slot, so everything here
/// is anchored on that: text justifies at y = 0.5, bands and rules are centred.
///
/// Widths are measured across a list, and one row of it carries a run:
/// SessionBests holds exactly one segment, practicing another checkpoint drops
/// the previous one. That is deliberate, decided 2026-08-28: the alternative was to key
/// SessionBests by segment and let a session accumulate rows, and it was
/// refused. One segment per export stays the rule, and the table stays with it
/// because features to come will want it. **Do not "simplify" this away** on
/// the grounds that the list is always length one; a review has already
/// proposed exactly that, and the answer was no.
internal sealed class ExportColumns {
    public const float Gap = 18f;

    // the journal's table scale; 0.6 is TextMenu.SubHeader's, sized for menu
    // chrome rather than for a forty-row table
    private const float Scale = 0.5f;
    private const float BandRatio = 0.9f;   // a gutter survives between stripes
    private const float RuleHeight = 2f;
    private const float BoxRatio = 0.45f;

    public static float RowHeight => ActiveFont.LineHeight * Scale * 1.2f;

    public float LabelX { get; private init; }
    public float RemoteX { get; private init; }
    public float LocalX { get; private init; }
    public float DeltaX { get; private init; }
    public float TotalWidth => DeltaX;

    /// x is the left edge for the label column, the right edge for the times:
    /// digits only line up when they are anchored on the right.
    public static void Text(string text, Vector2 position, float x, Color color, float alpha, bool left = false) {
        ActiveFont.DrawOutline(text, position + new Vector2(x, 0f),
            new Vector2(left ? 0f : 1f, 0.5f), Vector2.One * Scale, color,
            2f, Color.Black * (alpha * alpha * alpha));
    }

    public static void Band(Vector2 position, float width, Color color) {
        float height = RowHeight * BandRatio;
        Draw.Rect(position.X, MathF.Floor(position.Y - height / 2f), width, height, color);
    }

    public static void Rule(Vector2 position, float width, float offsetY, float alpha) {
        Draw.Rect(position.X, MathF.Floor(position.Y + offsetY - RuleHeight / 2f),
            width, RuleHeight, Color.White * (0.3f * alpha));
    }

    /// The atlas has no tick sprite. An outlined box that fills when ticked is
    /// what reads as a checkbox; dot_outline reads as a bullet.
    public static void Checkbox(Vector2 position, bool ticked, Color color) {
        float size = RowHeight * BoxRatio;
        float x = position.X;
        float y = MathF.Floor(position.Y - size / 2f);
        Draw.HollowRect(x, y, size, size, color);
        if (ticked) {
            float inset = size * 0.25f;
            Draw.Rect(x + inset, y + inset, size - inset * 2f, size - inset * 2f, color);
        }
    }

    private static float ArrowWidth => Width("<");

    /// The "< label >" affordance vanilla's Option draws, on the label cell.
    /// Both are drawn from their left edge, so the left one is pulled back by
    /// its own width to leave a real gap rather than butt against the checkbox.
    public static void ArrowsAt(Vector2 position, float x, string label, Color color, float alpha) {
        Text("<", position, x - Gap - ArrowWidth, color, alpha, left: true);
        Text(">", position, x + Width(label) + Gap, color, alpha, left: true);
    }

    public static float TextWidth(string text) => Width(text);

    /// Centred on the whole menu rather than aligned with the label column: the
    /// view's own controls head the table, they are not a row of it.
    public static void Centered(string text, Vector2 position, float width, Color color, float alpha) =>
        Text(text, position, (width - Width(text)) / 2f, color, alpha, left: true);

    private static float Width(string text) => ActiveFont.Measure(text).X * Scale;

    public static ExportColumns Measure(List<PendingUpdate> updates) {
        List<string> labels = [];
        foreach (PendingUpdate u in updates) {
            labels.Add(u.Label);
        }

        // floors, so a column does not resize when the fetch lands and the
        // sheet column goes from "" to real times
        float floor = Width("00:00.000");
        // no floor on the label column: the three time columns have a header to
        // stay at least as wide as, this one has none, and flooring it on
        // another column's header was a copy-paste that only made it wide
        float label = 0f;
        float remote = Math.Max(floor, Width(Dialog.Clean("SRS_EXPORT_COL_SHEET")));
        float local = Math.Max(floor, Width(Dialog.Clean("SRS_EXPORT_COL_LOCAL")));
        float delta = Math.Max(floor, Width(Dialog.Clean("SRS_EXPORT_COL_DELTA")));
        foreach (string text in labels) {
            label = Math.Max(label, Width(text));
        }
        // the trailing arrow lives inside the label column, so the times never
        // move when it appears; the leading one is budgeted in labelX below
        label += Gap + ArrowWidth;
        foreach (PendingUpdate u in updates) {
            remote = Math.Max(remote, Width(u.RemoteText));
            local = Math.Max(local, Width(u.LocalText));
            delta = Math.Max(delta, Width(u.DeltaText));
        }
        float labelX = RowHeight * BoxRatio + Gap + ArrowWidth + Gap;
        float remoteX = labelX + label + Gap + remote;   // right edge
        float localX = remoteX + Gap + local;            // right edge
        return new ExportColumns {
            LabelX = labelX,
            RemoteX = remoteX,
            LocalX = localX,
            DeltaX = localX + Gap + delta,               // right edge
        };
    }
}

/// Phase "review screen": lets the player pick which of this session's PBs to
/// push to the sheet before anything is written. Opened with a hotkey
/// (Hotkeys.OpenExportMenu), like a mini pause-menu of its own; it pauses the
/// level, so that same hotkey cannot close it. The menu's own Cancel button,
/// Back/ESC and the pause key do (OnCancel/OnESC/OnPause). Loaded last, and it
/// has to stay after Hotkeys: it reads OpenExportMenu.Pressed on the frame
/// Hotkeys produced it. Nothing else constrains it, it only reads what
/// ExportSource, RemoteBests, ExportClient and ExportProtocol produced.
internal static class ExportMenu {
    private const string LogTag = "srs";

    private static TextMenu menu;

    // the Level the menu is currently open on, and whether that level was
    // already paused before Open() forced it, so Close() restores the exact
    // prior state instead of unconditionally clearing Paused.
    //
    // Holding a Level across frames is what CLAUDE.md forbids, and this is the
    // deliberate exception: nothing replaces the level while the screen is up.
    // A savestate load was the way through we expected and is not one, but the
    // reason is SpeedrunTool's rather than ours, so the guarantee is only as
    // good as their code (see OnLevelUpdate). OnLevelUpdate closes on
    // menu.Scene != self against a path nobody has found rather than one
    // anybody has seen
    private static Level openLevel;
    private static bool pausedBeforeOpen;

    // the data currently backing the open menu; rebuilt wholesale (never
    // mutated in place) so Build() stays the single place that constructs rows
    private static List<PendingUpdate> currentUpdates;

    // both flags are set from ContinueWith callbacks (thread-pool threads) and
    // consumed on the game thread by the Level.Update hook — TextMenu must
    // never be touched off the game thread
    private static volatile bool queuedRebuild;
    // set by the category control; consumed on the game thread like the rest,
    // and a full rebuild rather than a refresh because "All" changes the row
    // count and the menu items are built one per row
    private static volatile bool queuedRecollect;

    // what the table is a view of. Null is "All", which lists every variant
    // instead of one route's. Neither this nor the route is ever written to
    // SrsSettings: the screen is a way of looking at the sheet, not a way of
    // setting the mod
    private static string viewCategory;
    private static int routeIndex;

    // true once the player has picked a view for themselves. Until then the
    // route their own sheet records is applied when the fetch brings it back,
    // which lands after the screen is already up
    private static bool viewChosenByPlayer;

    private static SheetRoute[] Routes => viewCategory == null ? [] : SheetRoutes.Of(viewCategory);

    /// the route the table is filtered to, null where the category itself is
    /// "All" and every variant of the chapter shows
    private static SheetRoute CurrentRoute =>
        Routes is { Length: > 0 } routes ? routes[Math.Clamp(routeIndex, 0, routes.Length - 1)] : null;

    // the row the cursor was on, kept across the rebuild a view change triggers
    private static int RestoreSelection(int selection) =>
        menu == null ? 0 : Math.Clamp(selection, 0, menu.Items.Count - 1);

    private static volatile bool queuedSummary;
    private static List<string> summaryLines;

    // guards against double-submitting while a POST is in flight
    private static volatile bool submitting;

    // bumped by every Open(). Close() cancels nothing in flight, so opening,
    // waiting, closing and reopening leaves two fetches racing into the same
    // state with no order guaranteed. The one that hurts is a Fail() from the
    // first arriving after the second succeeded: the screen has its rows and
    // Export is greyed out for good, over data that came back fine
    private static volatile int generation;

    public static void Load() {
        ExportProtocol.Localize = key => Dialog.Clean(key);

        On.Celeste.Level.Update += OnLevelUpdate;
    }

    public static void Unload() {
        On.Celeste.Level.Update -= OnLevelUpdate;
        Close();
    }

    private static void OnLevelUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // a level replaced under an open screen leaves the menu an entity of
        // the old one: it vanishes from the display while `menu` stays non
        // null, and Open() refuses for the rest of the session. No path there
        // is known. The savestate load we expected to be one is refused by
        // SpeedrunTool itself, not held back by the pause: read off 3.27.17,
        // its hotkeys are driven from MInput.Update and fire while paused, and
        // it is SaveLoadHotkeys that gates each action on !scene.Paused. That
        // is their check, in a dependency everest.yaml pins only a minimum of,
        // so this stays as insurance against it changing
        if (menu != null && menu.Scene != self) {
            Close();
        }

        // switched off with the screen open: close it instead of leaving a menu
        // behind that could still submit an export while the mod is inert
        if (!SrsModule.Settings.Enabled) {
            if (menu != null) {
                Close();
            }

            return;
        }

        // Hotkeys already holds the combo at rest while the level is paused, so
        // this never fires from behind the pause menu — nor from behind this
        // screen, which pauses the level itself
        if (Hotkeys.OpenExportMenu.Pressed) {
            Open(self);
        }

        // the menu may have been closed by the player between a background
        // task finishing and this frame running; nothing to do then
        if (queuedRecollect) {
            queuedRecollect = false;
            if (menu != null) {
                // the cursor stays where the player left it: changing the view
                // is not a reason to send them back to the top of the table
                int selection = menu.Selection;
                Build(self, ExportSource.Collect(self.Session, CurrentRoute));
                if (menu != null) {
                    menu.Selection = RestoreSelection(selection);
                }
            }
        }

        if (queuedRebuild) {
            queuedRebuild = false;
            // the fetch is what carries the player's own route, so this is the
            // first moment the default can be right. Moving the view means the
            // rows are the wrong ones, not merely stale
            bool moved = AdoptTheSheetsRoute();
            if (moved) {
                Logger.Log(LogLevel.Info, LogTag, $"view moved to the route the sheet records: {CurrentRoute?.Name}");
            }

            if (menu != null && currentUpdates != null) {
                Build(self, moved
                    ? ExportSource.Collect(self.Session, CurrentRoute)
                    : RefreshRemote(currentUpdates));
            }
        }

        if (queuedSummary) {
            queuedSummary = false;
            if (menu != null) {
                List<string> lines = summaryLines;
                summaryLines = null;
                ShowSummary(self, lines);
            }
        }
    }

    public static void Open(Level level) {
        if (menu != null) {
            return;
        }

        // before Collect, not after: a row built against the previous fetch
        // keeps that value for the whole session, because RefreshRemote only
        // fills the rows it could not fill the first time. The sheet has moved
        // since, at least because this screen wrote to it, and possibly because
        // the player corrected a row by hand in the browser
        RemoteBests.BeginFetch();

        OpenOnTheModsOwnView();
        List<PendingUpdate> updates = ExportSource.Collect(level.Session, CurrentRoute);
        Logger.Log(LogLevel.Info, LogTag,
            $"export view: {SegmentAutoDetect.ChapterOf(level.Session)} scope={SegmentAutoDetect.ScopeOf(level.Session)}"
            + $" category={viewCategory ?? "All"} route={CurrentRoute?.Name ?? "-"}"
            + $" rows={updates.Count} withRun={updates.Count(u => u.HasLocal)} held={SessionBests.Describe()}");
        if (updates.Count == 0) {
            RemoteBests.Reset();
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_NOTHING"), null);
            return;
        }

        int fetch = ++generation;
        string url = SrsModule.Settings.ExportUrl;
        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            if (fetch != generation) {
                return;
            }

            (string body, string error) = task.Result;
            if (error != null) {
                RemoteBests.Fail(error);
            } else if (ExportProtocol.TryParseRows(body, out List<RemoteRow> rows,
                           out List<RemoteRoute> known, out string parseError)) {
                RemoteBests.Accept(rows, known);
                Logger.Log(LogLevel.Info, LogTag,
                    $"sheet answered: {rows.Count} rows, routes ["
                    + string.Join(", ", known.Select(r => $"{r.Category}={r.Route}")) + "]");
            } else {
                RemoteBests.Fail(parseError);
            }
            // rebuild on the game thread: the rows' remote column depends on this
            queuedRebuild = true;
        });

        pausedBeforeOpen = level.Paused;
        openLevel = level;
        level.Paused = true;
        Build(level, updates);
    }

    public static void Close() {
        menu?.RemoveSelf();
        menu = null;
        currentUpdates = null;

        // stale flags from this session must never bleed into a later one:
        // if a background fetch/submit resolves after Close(), its queued
        // rebuild/summary would otherwise fire against a freshly reopened menu
        queuedRebuild = false;
        queuedRecollect = false;
        queuedSummary = false;
        summaryLines = null;
        // a POST may still be in flight; the generation check in its
        // continuation is what keeps it from touching whatever comes next
        submitting = false;

        if (openLevel != null) {
            openLevel.Paused = pausedBeforeOpen;
            openLevel = null;
        }
    }

    // picks up remote bests that arrived after the rows were first built. Does
    // not preserve a player's checkbox choice on a row that gains a remote
    // value: WillImprove is recomputed, which is what should drive the default
    // selection once the real comparison becomes possible. Rows that already
    // resolved are untouched, so a mid-review toggle on those is never
    // clobbered by a later rebuild. "Resolved" includes a cell we could not
    // read: that is an answer about the sheet, not a missing one
    private static List<PendingUpdate> RefreshRemote(List<PendingUpdate> updates) {
        List<PendingUpdate> refreshed = new(updates.Count);
        foreach (PendingUpdate u in updates) {
            if (u.RemoteTicks == null && !u.RemoteUnreadable && RemoteBests.TryGet(u.Row, out RemoteRow row)) {
                refreshed.Add(PendingUpdate.Create(u.Row, u.Label, u.LocalTicks, row.Time, u.Segment));
            } else {
                refreshed.Add(u);
            }
        }
        return refreshed;
    }

    /// Puts a screen up in place of whatever is there. Every screen this class
    /// shows goes through here: the three ways out of a menu are wired in one
    /// place rather than repeated, which is what stops a new screen from
    /// forgetting one and trapping the player in a paused level.
    ///
    /// backing is the row list Submit reads, and null for a screen with none.
    private static void Show(Level level, TextMenu newMenu, List<PendingUpdate> backing) {
        newMenu.OnCancel = Close;
        newMenu.OnESC = Close;
        newMenu.OnPause = Close;

        menu?.RemoveSelf();
        level.Add(newMenu);
        menu = newMenu;
        currentUpdates = backing;
    }

    private static void Build(Level level, List<PendingUpdate> updates) {
        ExportColumns columns = ExportColumns.Measure(updates);
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_TITLE")));
        newMenu.Add(new TextMenu.SubHeader(StatusLine()));

        newMenu.Add(new ViewControls(columns, CategoryLabel, CycleCategory));
        // its own line, and only where the category has more than one way through
        if (Routes.Length > 1) {
            newMenu.Add(new ViewControls(columns, RouteLabel, CycleRoute));
        }

        string chapter = null;
        bool odd = false;
        for (int i = 0; i < updates.Count; i++) {
            PendingUpdate update = updates[i];
            string group = string.IsNullOrEmpty(update.Row.Chapter) ? update.Row.Tab : update.Row.Chapter;
            if (group != chapter) {
                chapter = group;
                newMenu.Add(new GroupRow(columns, group));
            }

            newMenu.Add(new UpdateRow(updates, i, columns, odd));
            odd = !odd;
        }

        newMenu.Add(new TableFooter(columns));

        TextMenu.Button exportButton = new(ExportLabel(updates)) { Disabled = true };
        exportButton.OnUpdate = () => {
            exportButton.Label = ExportLabel(updates);
            // most rows are the sheet shown back, with nothing to write: the
            // button stays dead until something is actually ticked
            exportButton.Disabled = !RemoteBests.IsResolved || !updates.Any(u => u.Selected);
        };
        exportButton.Pressed(() => Submit(level, updates));
        newMenu.Add(exportButton);

        TextMenu.Button cancelButton = new(Dialog.Clean("SRS_EXPORT_CANCEL"));
        cancelButton.Pressed(Close);
        newMenu.Add(cancelButton);

        Show(level, newMenu, updates);
    }

    // the sheet's categories, then All: the routes read as the list and All as
    // the way of stepping outside it
    private static string[] ViewModes => [..SheetRoutes.Categories, null];

    /// opens on what the mod itself thinks the player is running, so the table
    /// agrees with the tier row under the timer until the player says otherwise
    private static void OpenOnTheModsOwnView() {
        // DTS is a route now, not a category: both True Ending settings open
        // on the same category, and the route below decides which of them
        viewCategory = SrsModule.Settings.Category switch {
            SegmentCategory.TrueEnding or SegmentCategory.TrueEndingDts => "True Ending",
            _ => "Any%",
        };

        // the first route of the category until the player's own sheet says
        // which one they run, which arrives with the fetch
        routeIndex = 0;
        viewChosenByPlayer = false;
        AdoptTheSheetsRoute();
    }

    private static string CategoryLabel() =>
        $"{Dialog.Clean("SRS_EXPORT_CATEGORY")}  " +
        (viewCategory ?? Dialog.Clean("SRS_EXPORT_CATEGORY_ALL"));

    private static string RouteLabel() =>
        $"{Dialog.Clean("SRS_EXPORT_ROUTE")}  {CurrentRoute?.Name}";

    /// The route the player records on their own sheet, once the fetch has
    /// brought it back. Their own pick always wins: this only ever fills in a
    /// default they have not overridden.
    ///
    /// True when it actually moved the view, which means the rows have to be
    /// collected again rather than merely refreshed.
    private static bool AdoptTheSheetsRoute() {
        if (viewChosenByPlayer) {
            return false;
        }

        string recorded = RemoteBests.RouteFor(viewCategory);
        int at = recorded == null
            ? -1
            : Array.FindIndex(Routes, route =>
                string.Equals(route.Name, recorded, StringComparison.OrdinalIgnoreCase));
        if (at < 0 || at == routeIndex) {
            return false;
        }

        routeIndex = at;
        return true;
    }

    private static void CycleCategory(int direction) {
        string[] modes = ViewModes;
        int at = Array.IndexOf(modes, viewCategory);
        viewCategory = modes[(at + direction + modes.Length) % modes.Length];
        routeIndex = 0;
        // a category the player chose still takes its route from the sheet:
        // only picking a route is a statement about the route
        AdoptTheSheetsRoute();
        queuedRecollect = true;
    }

    private static void CycleRoute(int direction) {
        int count = Routes.Length;
        if (count > 0) {
            routeIndex = (routeIndex + direction + count) % count;
            viewChosenByPlayer = true;
            queuedRecollect = true;
        }
    }

    // the sheet's own labels, never translated. Most checkpoint labels already
    // carry their chapter ("1a Start"), so prefixing it again reads "1a 1a Start"
    private static string RowLabel(ExportResult r) {
        string group = string.IsNullOrEmpty(r.Chapter) ? r.Tab : r.Chapter;
        return r.Cp.StartsWith(group, StringComparison.Ordinal) ? r.Cp : $"{group} {r.Cp}";
    }

    // an unknown status is shown as the script sent it rather than swallowed
    private static string StatusText(string status) => status switch {
        "written" => Dialog.Clean("SRS_EXPORT_STATUS_WRITTEN"),
        "notFound" => Dialog.Clean("SRS_EXPORT_STATUS_NOTFOUND"),
        "ambiguous" => Dialog.Clean("SRS_EXPORT_STATUS_AMBIGUOUS"),
        "refused" => Dialog.Clean("SRS_EXPORT_STATUS_REFUSED"),
        _ => status,
    };

    private static string ExportLabel(List<PendingUpdate> updates) =>
        $"{Dialog.Clean("SRS_EXPORT_CONFIRM")} ({updates.Count(u => u.Selected)})";

    // reflects RemoteBests.State: Loading/Error get their own message, Ready
    // (or NotLoaded, which cannot really happen here since BeginFetch runs
    // before the first Build) shows the column legend instead
    // empty once the fetch resolves: the chapter bands carry the column titles
    // from then on, aligned with the rows, which a free-text SubHeader cannot be
    private static string StatusLine() => RemoteBests.State switch {
        RemoteState.Loading => Dialog.Clean("SRS_EXPORT_LOADING"),
        RemoteState.Error => RemoteBests.Error ?? Dialog.Clean("SRS_EXPORT_UNREAD"),
        _ => "",
    };

    private static void Submit(Level level, List<PendingUpdate> updates) {
        if (submitting) {
            // saying nothing here read as a dead button, and it could last the
            // whole 60 s timeout
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_WRITING"), null);
            return;
        }

        // rows are pre-selected as "improves" while the fetch is still in flight
        // (remoteTicks == null) or after it failed, and submitting in either
        // state risks overwriting a good sheet time with a worse local one,
        // since the comparison was never actually made. Unreachable as things
        // stand, the Export button is Disabled on the same condition and a
        // disabled item cannot be pressed; kept because it guards a data-loss
        // path and costs nothing, but logging rather than popping a message no
        // player is in a position to read
        if (!RemoteBests.IsResolved) {
            Logger.Log(LogLevel.Warn, LogTag, "submit reached the unresolved guard: " + RemoteBests.State);
            return;
        }

        List<PendingUpdate> selected = updates.Where(u => u.Selected).ToList();
        if (selected.Count == 0) {
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_NOTHING"), null);
            return;
        }

        submitting = true;
        int submission = generation;

        ExportRequest request = new() {
            Updates = selected.Select(u => new ExportUpdate {
                Tab = u.Row.Tab,
                Chapter = u.Row.Chapter,
                Cp = u.Row.Cp,
                Time = TimeFormat.FromTicks(u.LocalTicks),
            }).ToList(),
        };
        string json = ExportProtocol.SerializeRequest(request);
        string url = SrsModule.Settings.ExportUrl;

        Logger.Log(LogLevel.Info, LogTag,
            $"exporting {request.Updates.Count} row(s)");

        // swap to a "working..." placeholder while the POST is in flight; this
        // runs on the game thread already (a button press), so no queueing needed
        ShowWorking(level);

        _ = ExportClient.PostAsync(url, json).ContinueWith(task => {
            if (submission != generation) {
                // the screen this belonged to was closed and another opened. The
                // write did happen and its outcome is in the log; there is no
                // longer anyone to show it to, and clearing `submitting` here
                // would open the double-submit guard on the newer screen
                Logger.Log(LogLevel.Info, LogTag, "export resolved after its screen was replaced");
                return;
            }

            (string body, string error) = task.Result;
            submitting = false;

            if (error != null) {
                Logger.Log(LogLevel.Warn, LogTag, "export failed: " + error);
                QueueSummary([error]);
                return;
            }

            if (!ExportProtocol.TryParseResponse(body, out ExportResponse response, out string parseError)) {
                Logger.Log(LogLevel.Warn, LogTag, "unreadable answer: " + parseError);
                QueueSummary([parseError]);
                return;
            }

            // the status is translated, the script's own reason is not, on
            // purpose: it names the row and the cause, we do not author it, and
            // a report pasted into an issue has to carry its words whatever the
            // player's language
            foreach (ExportResult r in response.Results) {
                Logger.Log(LogLevel.Info, LogTag, $"  {RowLabel(r)}: {r.Status}" +
                    (string.IsNullOrEmpty(r.Reason) ? "" : $" ({r.Reason})"));
            }

            List<string> lines = response.Results
                .Select(r => $"{RowLabel(r)}: {StatusText(r.Status)}" +
                    (string.IsNullOrEmpty(r.Reason) ? "" : $" ({r.Reason})"))
                .ToList();
            if (lines.Count == 0) {
                lines.Add(Dialog.Clean("SRS_EXPORT_DONE"));
            }
            QueueSummary(lines);
        });
    }

    private static void QueueSummary(List<string> lines) {
        summaryLines = lines;
        queuedSummary = true;
    }

    private static void ShowWorking(Level level) {
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_TITLE")));
        // not SRS_EXPORT_LOADING: that one belongs to the fetch, and announcing
        // a read while the sheet is being written to is the wrong promise
        newMenu.Add(new TextMenu.SubHeader(Dialog.Clean("SRS_EXPORT_WRITING")));

        Show(level, newMenu, null);
    }

    // replaces the menu contents with a plain-text, line-per-row summary of
    // the export result plus a single Close button; reached only from
    // OnLevelUpdate on the game thread
    private static void ShowSummary(Level level, List<string> lines) {
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_DONE")));
        foreach (string line in lines) {
            newMenu.Add(new TextMenu.SubHeader(line, topPadding: false));
        }

        // not "Cancel": the rows above are already written, and offering to
        // cancel them is a promise this screen cannot keep
        TextMenu.Button closeButton = new(Dialog.Clean("SRS_EXPORT_CLOSE"));
        closeButton.Pressed(Close);
        newMenu.Add(closeButton);

        Show(level, newMenu, null);
    }
}
