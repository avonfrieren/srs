using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    // the list Submit reads
    private readonly List<PendingUpdate> slot;
    private readonly int index;

    public PendingUpdate Update => slot[index];

    public UpdateRow(List<PendingUpdate> slot, int index, ExportColumns columns, bool odd) {
        this.slot = slot;
        this.index = index;
        this.columns = columns;
        this.odd = odd;
        // false on the base item: without it the cursor never lands on the row
        Selectable = true;
    }

    public override void ConfirmPressed() {
        Update.Selected = !Update.Selected;
        Audio.Play(Update.Selected ? "event:/ui/main/button_toggle_on" : "event:/ui/main/button_toggle_off");
    }

    public override float LeftWidth() => columns.TotalWidth;
    public override float Height() => ExportColumns.RowHeight;

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        PendingUpdate update = Update;

        // both parities banded: one stripe over bare background reads as a
        // tinted list, two read as a grid
        Color band = highlighted
            ? Color.White * (0.22f * alpha)
            : Color.White * ((odd ? 0.09f : 0.04f) * alpha);
        ExportColumns.Band(position, Container.Width, band);

        ExportColumns.Checkbox(position, update.Selected, Color.White * alpha);

        Color text = Color.White * alpha;
        ExportColumns.Text(update.Label, position, columns.LabelX, text, alpha, left: true);
        ExportColumns.Text(update.RemoteText, position, columns.RemoteX, Color.Gray * alpha, alpha);
        ExportColumns.Text(update.LocalText, position, columns.LocalX, text, alpha);
        ExportColumns.Text(update.DeltaText, position, columns.DeltaX, DeltaColor(update) * alpha, alpha);
    }

    // neutral when there is nothing to compare against and when the times are
    // equal: "+0.000" in red says a regression that did not happen. An
    // unreadable cell lands here through RemoteTicks staying null, and its "?"
    // is a refusal rather than a regression
    private static Color DeltaColor(PendingUpdate update) {
        if (update.RemoteTicks == null || update.LocalTicks == update.RemoteTicks.Value) {
            return Color.Gray;
        }

        return update.LocalTicks < update.RemoteTicks.Value ? Ahead : Behind;
    }
}

/// The column titles, over the chapter they belong to. TextMenu moves the whole
/// menu when it scrolls, so a single header at the top goes with it; repeating
/// it is what a grouped spreadsheet does.
internal sealed class GroupRow(ExportColumns columns, string label) : TextMenu.Item {
    public override float LeftWidth() => columns.TotalWidth;
    public override float Height() => ExportColumns.RowHeight;

    public override void Render(Vector2 position, bool highlighted) {
        float alpha = Container.Alpha;
        Color color = Color.Gray * alpha;

        ExportColumns.Rule(position, Container.Width, -Height() / 2f, alpha);
        ExportColumns.Rule(position, Container.Width, Height() / 2f, alpha);

        // the chapter the rows below belong to: the row labels have dropped it
        // on the folded chapters, and never carried it on Farewell
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
/// ⚠️ Widths are measured across a list even though the screen shows one row
/// for now. Do not collapse the geometry to a single row: the list is what
/// the next feature needs (owner decision).
internal sealed class ExportColumns {
    public const float Gap = 18f;

    // the table's own margin: the banding and rules span the whole menu, so
    // without it the checkbox and the delta column touch its edges
    private const float Pad = 24f;

    // the journal's table scale. TextMenu.SubHeader's 0.6 is sized for menu
    // chrome, not for a table
    private const float Scale = 0.5f;
    private const float BandRatio = 0.9f;   // a gutter survives between stripes
    private const float RuleHeight = 2f;
    private const float BoxRatio = 0.45f;

    public static float RowHeight => ActiveFont.LineHeight * Scale * 1.2f;

    public float LabelX { get; private init; }
    public float RemoteX { get; private init; }
    public float LocalX { get; private init; }
    public float DeltaX { get; private init; }
    public float TotalWidth => DeltaX + Pad;

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
        float x = position.X + Pad;
        float y = MathF.Floor(position.Y - size / 2f);
        Draw.HollowRect(x, y, size, size, color);
        if (ticked) {
            float inset = size * 0.25f;
            Draw.Rect(x + inset, y + inset, size - inset * 2f, size - inset * 2f, color);
        }
    }

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

        foreach (PendingUpdate u in updates) {
            remote = Math.Max(remote, Width(u.RemoteText));
            local = Math.Max(local, Width(u.LocalText));
            delta = Math.Max(delta, Width(u.DeltaText));
        }
        float labelX = Pad + RowHeight * BoxRatio + Gap;
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

/// The review screen: which of this session's times to push to the sheet, with
/// nothing written before it is confirmed. Opened and closed by the same hotkey;
/// it pauses the level, and Hotkeys reads HoldsThePause to keep that one combo
/// alive behind the pause it caused. Cancel, Back/ESC and pause close it too.
///
/// ⚠️ Must load after Hotkeys: it reads OpenExportMenu.Pressed on the frame
/// Hotkeys produced it.
internal static class ExportMenu {
    private const string LogTag = "srs";

    private static TextMenu menu;

    // the Level the screen is open on, and whether it was already paused before
    // Open() forced it, so Close() restores the prior state.
    //
    // ⚠️ A Level is normally never held across frames: the scene can be
    // replaced between two of them. Nothing replaces it while the screen is up, and that guarantee is
    // SpeedrunTool's rather than ours; OnLevelUpdate closes on menu.Scene != self
    private static Level openLevel;
    private static bool pausedBeforeOpen;

    // read by Hotkeys: the level is paused because this screen paused it, so
    // the hotkey that opened it must keep being read in order to close it
    internal static bool HoldsThePause => openLevel != null;

    // the screen is up showing "loading": the table is built by the fetch
    // landing. Read from a worker by Refresh, which must not start one behind it
    private static volatile bool awaitingRows;

    // a background refresh is in flight. Only one at a time; one asked for
    // meanwhile is kept and run when it lands, because the one after a write
    // is the only one asking for the cells as they are now. Both under
    // refreshGate: they are handed over between the game thread and a worker
    private static readonly object refreshGate = new();
    private static bool refreshing;
    private static string refreshAgain;

    // bumped when a POST answers. A read started before that may hold the
    // cells as they were, and taking it in would show the row as an
    // improvement again, for the write to refuse it as changed
    private static int writes;

    // how stale a held answer has to be before opening the screen asks again
    private static readonly TimeSpan AskAgainAfter = TimeSpan.FromSeconds(60);

    // both flags are set from ContinueWith callbacks (thread-pool threads) and
    // consumed on the game thread by the Level.Update hook — TextMenu must
    // never be touched off the game thread
    private static volatile bool queuedRebuild;
    private static volatile bool queuedSummary;
    private static List<string> summaryLines;

    // guards against double-submitting while a POST is in flight
    private static volatile bool submitting;

    // bumped by every Open(). Close() cancels nothing in flight, so reopening
    // races two fetches; the one that hurts is the first's Fail() landing after
    // the second succeeded, greying Export out over data that came back fine
    private static volatile int generation;

    public static void Load() {
        // Dialog loads after the mods do, and the launch refresh below can be
        // answered before it: Dialog.Clean then throws on a null Language, which
        // turned a plain 404 at boot into an exception. The key is logged instead
        ExportProtocol.Localize = key => Dialog.Language == null ? key : Dialog.Clean(key);

        On.Celeste.Level.Update += OnLevelUpdate;

        // about a second of the round trip is Google's dispatch whatever the
        // script does; starting here is what opens the screen on data
        Refresh("launch");
    }

    /// A refresh nobody is waiting on: no generation, no rebuild, and a failure
    /// keeps what we hold. The URL it asked stands in for the generation, and is
    /// rechecked when the answer lands. Safe to let land under an open screen,
    /// which holds the rows it was built from and the values a write compares
    /// against (ExportUpdate.Expect).
    internal static void Refresh(string why) {
        string url = ExportTarget.Url;
        if (!SrsModule.Settings.Enabled || awaitingRows || string.IsNullOrWhiteSpace(url)) {
            return;
        }

        lock (refreshGate) {
            if (refreshing) {
                refreshAgain = why;
                return;
            }

            refreshing = true;
        }

        int writesBefore = Volatile.Read(ref writes);
        Logger.Log(LogLevel.Info, LogTag, "refreshing the sheet in the background: " + why);
        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            string again = null;
            try {
                if (writesBefore != Volatile.Read(ref writes)) {
                    Logger.Log(LogLevel.Info, LogTag,
                        "a background refresh read the sheet before the latest export; dropped");
                    return;
                }

                // repointed or forgotten from Mod Options while this was out:
                // taking it in would resolve RemoteBests against another sheet
                if (url != ExportTarget.Url) {
                    Logger.Log(LogLevel.Info, LogTag,
                        "a background refresh answered for a sheet URL that is no longer the one set; dropped");
                    return;
                }

                Take(task.Result, ownedByAScreen: false);
            } catch (Exception e) {
                // no screen is waiting on this one, and nothing above it catches:
                // a throw here would leave the game with the exception
                Logger.Log(LogLevel.Warn, LogTag, "a background refresh could not be taken in: " + e);
            } finally {
                // never in the body: cleared nowhere else, so a throw skipping
                // it would silently kill every later refresh of the session
                lock (refreshGate) {
                    refreshing = false;
                    again = refreshAgain;
                    refreshAgain = null;
                }

                // here and not after the block: the dropped answer returns
                // early, and that is the case with a refresh waiting
                if (again != null) {
                    Refresh(again);
                }
            }
        });
    }

    /// Takes an answer in. Runs on a worker thread and writes nothing but
    /// RemoteBests, which is built for that. ownedByAScreen says whether someone
    /// is waiting: a screen turns a failure into its status line, a background
    /// refresh logs it and keeps what it holds.
    private static void Take((string body, string error) answer, bool ownedByAScreen) {
        // the master switch has to cover an answer to a question asked before it
        // was thrown, or the mod writes and announces itself while inert
        if (!SrsModule.Settings.Enabled) {
            Logger.Log(LogLevel.Info, LogTag, "the sheet answered after the mod was switched off; dropped");
            return;
        }

        (string body, string error) = answer;
        if (error != null) {
            Fail(error, ownedByAScreen);
            return;
        }

        if (!ExportProtocol.TryParseRows(body, out List<RemoteRow> rows,
                out string scriptTiming, out string parseError)) {
            Fail(parseError, ownedByAScreen);
            return;
        }

        RemoteBests.Accept(rows);
        Logger.Log(LogLevel.Info, LogTag, $"sheet answered: {rows.Count} rows, {scriptTiming}");
    }

    private static void Fail(string error, bool ownedByAScreen) {
        if (ownedByAScreen) {
            RemoteBests.Fail(error);
        } else {
            Logger.Log(LogLevel.Info, LogTag, "background refresh failed, keeping what we hold: " + error);
        }
    }

    public static void Unload() {
        On.Celeste.Level.Update -= OnLevelUpdate;
        Close();
    }

    private static void OnLevelUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // a level replaced under an open screen leaves the menu an entity of the
        // old one: it vanishes while `menu` stays non null and Open() refuses for
        // the rest of the session. No path there is known -- the savestate load is
        // refused by SpeedrunTool's own !scene.Paused gate (3.27.17), in a
        // dependency everest.yaml pins only a minimum of
        if (menu != null && menu.Scene != self) {
            // logged because nothing is known to trigger it: silent, the path
            // could neither be tested nor caught doing its job
            Logger.Log(LogLevel.Warn, LogTag, "the level was replaced under the export screen; closed it");
            Close();
        }

        // switched off with the screen open: a menu left behind could still
        // submit an export while the mod is inert
        if (!SrsModule.Settings.Enabled) {
            if (menu != null) {
                Close();
            }

            return;
        }

        // Hotkeys holds the combo at rest behind the pause menu but not behind
        // this screen's own pause: one press opens, the next closes
        if (Hotkeys.Pressed(Hotkeys.OpenExportMenu)) {
            if (menu != null) {
                Close();
            } else {
                Open(self);
            }
        }

        // the menu may have been closed by the player between the fetch
        // resolving and this frame running; nothing to do then
        if (queuedRebuild) {
            queuedRebuild = false;
            if (menu != null && awaitingRows) {
                Build(self, ExportSource.Collect(self.Session));
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

        // a wipe keeps running under the pause, and the one after a death ends
        // in Level.Reload, which removes the screen with every other entity
        if (level.Wipe != null) {
            return;
        }

        if (!ExportUrlMenu.HasUrl) {
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_NEEDS_URL"), null);
            return;
        }

        List<PendingUpdate> updates = ExportSource.Collect(level.Session);
        Logger.Log(LogLevel.Info, LogTag,
            $"export: scope={SegmentAutoDetect.ScopeOf(level.Session)}"
            + $" rows={updates.Count} held={SessionBests.Describe()}");
        // nothing run this session, or a run that maps to no row
        if (updates.Count == 0) {
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_NOTHING"), null);
            return;
        }

        pausedBeforeOpen = level.Paused;
        openLevel = level;
        level.Paused = true;
        // taken by every open: a POST from the previous screen may still be in
        // flight, and its continuation checks this to know its screen is gone
        int fetch = ++generation;

        // a refresh has answered: build now, with no wait. What is on screen can
        // be a refresh old, and the write is what guards against that -- it
        // compares each cell before touching it
        if (RemoteBests.IsResolved) {
            Build(level, updates);
            // opening, closing and reopening inside a minute is one action, and
            // asking three times costs three calls against the player's script
            if (RemoteBests.Age > AskAgainAfter) {
                Refresh("a screen opened on data already held");
            }

            return;
        }

        // nothing held: the first open of a session that launched offline, or
        // one whose refresh has not landed yet
        RemoteBests.BeginFetch();
        string url = ExportTarget.Url;
        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            if (fetch != generation) {
                // an older answer would overwrite a newer one. Logged for the
                // same reason as the guard above: silent, it cannot be seen work
                Logger.Log(LogLevel.Info, LogTag, "a fetch resolved after its screen was replaced; discarded");
                return;
            }

            // the generation only moves when a screen opens, and the sheet is
            // repointed from Mod Options with no screen up: closing and
            // forgetting the URL leaves the generation where it was, and this
            // answer would resolve RemoteBests against a sheet nobody points at
            if (url != ExportTarget.Url) {
                Logger.Log(LogLevel.Info, LogTag,
                    "a fetch resolved for a sheet URL that is no longer the one set; discarded");
                return;
            }

            try {
                Take(task.Result, ownedByAScreen: true);
            } catch (Exception e) {
                // a screen is waiting: with no state to show it sits on
                // "loading" until the player cancels
                Logger.Log(LogLevel.Warn, LogTag, "an answer could not be taken in: " + e);
                RemoteBests.Fail(Dialog.Clean("SRS_EXPORT_UNREAD"));
            } finally {
                // build on the game thread: a TextMenu is never touched off it
                queuedRebuild = true;
            }
        });

        awaitingRows = true;
        ShowLoading(level);
    }

    public static void Close() {
        menu?.RemoveSelf();
        menu = null;
        awaitingRows = false;

        // a fetch or submit resolving after Close() would otherwise fire its
        // queued rebuild against a freshly reopened menu
        queuedRebuild = false;
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

    /// Puts a screen up in place of whatever is there. Every screen goes through
    /// here so the three ways out are wired once: a screen forgetting one traps
    /// the player in a paused level.
    private static void Show(Level level, TextMenu newMenu) {
        newMenu.OnCancel = Close;
        newMenu.OnESC = Close;
        newMenu.OnPause = Close;

        menu?.RemoveSelf();
        level.Add(newMenu);
        menu = newMenu;
    }

    /// keepSelection is the row the cursor was on, for a rebuild that leaves
    /// the table's shape alone. Without one the screen opens on the run itself.
    private static void Build(Level level, List<PendingUpdate> updates) {
        awaitingRows = false;
        ExportColumns columns = ExportColumns.Measure(updates);
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_TITLE")));
        newMenu.Add(new TextMenu.SubHeader(StatusLine()));

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

        // rows built on no answer compared against nothing, and an answer
        // landing later does not rebuild them: such a table never exports
        bool builtOnAnswer = RemoteBests.IsResolved;
        TextMenu.Button exportButton = new(ExportLabel(updates)) { Disabled = !builtOnAnswer };
        exportButton.OnUpdate = () => {
            exportButton.Label = ExportLabel(updates);
            exportButton.Disabled = !builtOnAnswer || !RemoteBests.IsResolved;
        };
        exportButton.Pressed(() => Submit(level, updates, builtOnAnswer));
        newMenu.Add(exportButton);

        TextMenu.Button cancelButton = new(Dialog.Clean("SRS_EXPORT_CANCEL"));
        cancelButton.Pressed(Close);
        newMenu.Add(cancelButton);

        Show(level, newMenu);
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
        "changed" => Dialog.Clean("SRS_EXPORT_STATUS_CHANGED"),
        _ => status,
    };

    private static string ExportLabel(List<PendingUpdate> updates) =>
        $"{Dialog.Clean("SRS_EXPORT_CONFIRM")} ({updates.Count(u => u.Selected)})";

    // empty once the fetch resolves: the chapter bands carry the column titles
    // from then on, aligned with the rows, which a SubHeader cannot be
    private static string StatusLine() => RemoteBests.State switch {
        RemoteState.Loading => Dialog.Clean("SRS_EXPORT_LOADING"),
        RemoteState.Error => RemoteBests.Error ?? Dialog.Clean("SRS_EXPORT_UNREAD"),
        _ => "",
    };

    private static void Submit(Level level, List<PendingUpdate> updates, bool builtOnAnswer) {
        if (submitting) {
            // saying nothing here read as a dead button, and it could last the
            // whole 60 s timeout
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_WRITING"), null);
            return;
        }

        // unresolved rows pre-select as "improves" without ever having been
        // compared, so submitting one can overwrite a better sheet time.
        // Unreachable (Export is Disabled on the same condition), kept because
        // it guards a data-loss path
        if (!builtOnAnswer || !RemoteBests.IsResolved) {
            Logger.Log(LogLevel.Warn, LogTag, "submit reached the unresolved guard: " + RemoteBests.State);
            return;
        }

        List<PendingUpdate> selected = updates.Where(u => u.Selected).ToList();
        if (selected.Count == 0) {
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_NONE_TICKED"), null);
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
                // the raw cell, so the script compares what the sheet displays
                // against itself: a reformat of it would differ on every row
                // the sheet writes short ("1:36.9")
                Expect = u.RemoteCell,
            }).ToList(),
        };
        string json = ExportProtocol.SerializeRequest(request);
        string url = ExportTarget.Url;

        Logger.Log(LogLevel.Info, LogTag,
            $"exporting {request.Updates.Count} row(s)");

        // swap to a "working..." placeholder while the POST is in flight; this
        // runs on the game thread already (a button press), so no queueing needed
        ShowWorking(level);

        _ = ExportClient.PostAsync(url, json).ContinueWith(task => {
            // first, before the screen check: a refresh out now read the
            // sheet before this write, whether or not its screen is still up
            Interlocked.Increment(ref writes);
            // on every answer, failed or orphaned too: the count just dropped
            // any refresh out, and a failed POST may still have written (a
            // timeout). Queued behind one already out, run when it lands
            Refresh("an export answered");
            if (submission != generation) {
                // the write happened and its outcome is in the log; nobody is
                // left to show it to, and clearing `submitting` here would open
                // the double-submit guard on the newer screen
                Logger.Log(LogLevel.Info, LogTag, "export resolved after its screen was replaced");
                return;
            }

            submitting = false;

            // nothing above this continuation observes a throw: the screen
            // would stay on "Writing..." until the player leaves it
            try {
                (string body, string error) = task.Result;
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

                // the status is translated, the script's own reason is not: we do
                // not author it, and a pasted report has to carry its words
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
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, LogTag, "an export answer could not be shown: " + e);
                QueueSummary([$"{Dialog.Clean("SRS_EXPORT_ERR_UNREADABLE")} {e.GetType().Name}"]);
            }
        });
    }

    private static void QueueSummary(List<string> lines) {
        summaryLines = lines;
        queuedSummary = true;
    }

    /// Up while the first fetch is in flight, in place of the table: rows built
    /// before the sheet answers compare against values that have not arrived and
    /// pre-tick as improvements, which reads as a finished table and is not one.
    private static void ShowLoading(Level level) {
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_TITLE")));
        newMenu.Add(new TextMenu.SubHeader(Dialog.Clean("SRS_EXPORT_LOADING")));

        // a way out without knowing Back closes it: the one screen the player
        // may want to leave before it has done anything
        TextMenu.Button cancelButton = new(Dialog.Clean("SRS_EXPORT_CANCEL"));
        cancelButton.Pressed(Close);
        newMenu.Add(cancelButton);

        Show(level, newMenu);
    }

    private static void ShowWorking(Level level) {
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_TITLE")));
        // not SRS_EXPORT_LOADING: that one belongs to the fetch, and announcing
        // a read while the sheet is being written to is the wrong promise
        newMenu.Add(new TextMenu.SubHeader(Dialog.Clean("SRS_EXPORT_WRITING")));

        Show(level, newMenu);
    }

    // a line-per-row summary of the result plus a Close button; reached only
    // from OnLevelUpdate, on the game thread
    // a SubHeader draws on one line and never wraps. The script's own
    // reasons are long English sentences; the full text is in log.txt
    private const float SummaryMaxWidth = 1600f;

    private static string FitOnScreen(string line) {
        const float scale = TextMenu.SubHeader.Scale;
        if (ActiveFont.Measure(line).X * scale <= SummaryMaxWidth) {
            return line;
        }

        int keep = line.Length;
        while (keep > 0 && ActiveFont.Measure(line[..keep] + "...").X * scale > SummaryMaxWidth) {
            keep--;
        }

        return line[..keep].TrimEnd() + "...";
    }

    private static void ShowSummary(Level level, List<string> lines) {
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_DONE")));
        foreach (string line in lines) {
            newMenu.Add(new TextMenu.SubHeader(FitOnScreen(line), topPadding: false));
        }

        // not "Cancel": the rows above are already written, and offering to
        // cancel them is a promise this screen cannot keep
        TextMenu.Button closeButton = new(Dialog.Clean("SRS_EXPORT_CLOSE"));
        closeButton.Pressed(Close);
        newMenu.Add(closeButton);

        Show(level, newMenu);
    }
}
