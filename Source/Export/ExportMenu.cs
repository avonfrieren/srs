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
    // the list Submit reads: retargeting replaces this row's entry in it, so
    // reading through the list is what keeps the two in step
    private readonly List<PendingUpdate> slot;
    private readonly int index;

    private readonly List<SheetSegment> candidates;
    private readonly Session session;
    private int candidate;

    public PendingUpdate Update => slot[index];

    public UpdateRow(List<PendingUpdate> slot, int index, ExportColumns columns, bool odd, Session session) {
        this.slot = slot;
        this.index = index;
        this.columns = columns;
        this.odd = odd;
        this.session = session;
        candidates = ExportSource.CandidatesFor(slot[index].Segment, session);
        // by name, not by reference: SheetImporter.Data is reassigned from a
        // worker and a refresh landing between Collect and here hands out fresh
        // SheetSegment instances, which have no Equals. IndexOf would return -1
        // and left/right would die without a word, in the first minute of a
        // session since v3.5.0 refreshes on launch
        candidate = candidates.FindIndex(other => other.Name == slot[index].Segment?.Name);
        // base TextMenu.Item defaults this to false; without it the cursor
        // can never land on the row and ConfirmPressed() is never dispatched
        Selectable = true;
    }

    public override void ConfirmPressed() {
        Update.Selected = !Update.Selected;
    }

    // auto-detect cannot tell two segments sharing a start room apart; left and
    // right move the time onto another row anchored on the same checkpoint
    public override void LeftPressed() => Retarget(-1);
    public override void RightPressed() => Retarget(1);

    private bool CanRetarget => candidates.Count > 1 && candidate >= 0;

    private void Retarget(int direction) {
        if (!CanRetarget) {
            return;
        }

        candidate = (candidate + direction + candidates.Count) % candidates.Count;
        SheetSegment segment = candidates[candidate];
        if (!SheetLabels.TryMap(segment.Chapter, segment.Name, out SheetRowRef row)) {
            return;
        }

        PendingUpdate next = ExportSource.Build(row, segment, Update.LocalTicks, session);
        // carry the tick over, but never onto a row it would not improve: the
        // screen promises to start unticked when it would not, and one arrow
        // press must not be what takes that promise back
        next.Selected = Update.Selected && next.WillImprove;
        slot[index] = next;
        Audio.Play(direction < 0 ? "event:/ui/main/rollover_up" : "event:/ui/main/rollover_down");
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

        ExportColumns.Checkbox(position, update.Selected, Color.White * alpha);

        Color text = Color.White * alpha;
        ExportColumns.Text(update.Label, position, columns.LabelX, text, alpha, left: true);
        if (CanRetarget && highlighted) {
            ExportColumns.Arrows(position, columns, update.Label, text, alpha);
        }

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

    // the table's own margin. The banding and the rules span the whole menu, so
    // without it the checkbox sits on the left edge and the delta column ends on
    // the right one, both touching the grid they are inside
    private const float Pad = 24f;

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

    private static float ArrowWidth => Width("<");

    /// The "< label >" affordance vanilla's Option draws, on the label cell.
    /// Both are drawn from their left edge, so the left one is pulled back by
    /// its own width to leave a real gap rather than butt against the checkbox.
    public static void Arrows(Vector2 position, ExportColumns columns, string label, Color color, float alpha) {
        Text("<", position, columns.LabelX - Gap - ArrowWidth, color, alpha, left: true);
        Text(">", position, columns.LabelX + Width(label) + Gap, color, alpha, left: true);
    }

    private static float Width(string text) => ActiveFont.Measure(text).X * Scale;

    public static ExportColumns Measure(List<PendingUpdate> updates, Session session) {
        List<string> labels = [];
        foreach (PendingUpdate u in updates) {
            labels.Add(u.Label);
            // every row the arrows can reach, so the column does not resize
            // under a retarget
            foreach (SheetSegment other in ExportSource.CandidatesFor(u.Segment, session)) {
                labels.Add(ExportSource.DisplayName(other, session));
            }
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
        float labelX = Pad + RowHeight * BoxRatio + Gap + ArrowWidth + Gap;
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
/// push to the sheet before anything is written. Opened and closed with the
/// same hotkey (Hotkeys.OpenExportMenu), like a mini pause-menu of its own; it
/// pauses the level, and Hotkeys reads HoldsThePause to keep that one combo
/// alive behind the pause it caused. The menu's own Cancel button, Back/ESC and
/// the pause key close it too (OnCancel/OnESC/OnPause). Loaded last, and it
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

    // read by Hotkeys: the level is paused because this screen paused it, so
    // the hotkey that opened it must keep being read in order to close it
    internal static bool HoldsThePause => openLevel != null;

    // the screen is up but showing "loading": the table is not built until the
    // sheet has answered, and the fetch landing is what builds it. Read from a
    // worker thread by Refresh, which must not start one behind that wait
    private static volatile bool awaitingRows;

    // a background refresh is in flight. Only one at a time: they exist to have
    // an answer ready, and a second in the queue brings that no sooner
    private static volatile bool refreshing;

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

    // bumped by every Open(). Close() cancels nothing in flight, so opening,
    // waiting, closing and reopening leaves two fetches racing into the same
    // state with no order guaranteed. The one that hurts is a Fail() from the
    // first arriving after the second succeeded: the screen has its rows and
    // Export is greyed out for good, over data that came back fine
    private static volatile int generation;

    public static void Load() {
        ExportProtocol.Localize = key => Dialog.Clean(key);

        On.Celeste.Level.Update += OnLevelUpdate;

        // the screen's whole wait is one round trip, and about a second of it
        // is Google's dispatch whatever the script does. Starting it here is
        // what lets the screen open on data instead of on a loading line
        Refresh("launch");
    }

    /// A refresh nobody is waiting on. It takes no generation and queues no
    /// rebuild, and a failure leaves what we hold rather than replacing it with
    /// an error the player has no screen to read. What stands in for the
    /// generation is the URL it asked, rechecked when the answer lands.
    ///
    /// Safe to let land under an open screen: the table holds the rows it was
    /// built from, nothing rebuilds under the player, and the values a write
    /// compares against travel with it (ExportUpdate.Expect).
    internal static void Refresh(string why) {
        string url = SrsModule.Settings.ExportUrl;
        if (!SrsModule.Settings.Enabled || refreshing || awaitingRows
            || string.IsNullOrWhiteSpace(url)) {
            return;
        }

        refreshing = true;
        Logger.Log(LogLevel.Info, LogTag, "refreshing the sheet in the background: " + why);
        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            try {
                // the sheet can be repointed or forgotten from Mod Options while
                // this is out: taking the answer in would republish the old
                // sheet's times, and on the forget path would leave RemoteBests
                // resolved against a URL that no longer exists
                if (url != SrsModule.Settings.ExportUrl) {
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
                // never in the body: the flag is cleared nowhere else, so a throw
                // that skipped it would silently kill every later refresh of the
                // session
                refreshing = false;
            }
        });
    }

    /// Takes in an answer from the sheet. Runs on a worker thread, and writes
    /// nothing but RemoteBests, which is built to be written from one.
    ///
    /// ownedByAScreen says whether someone is waiting on this: a screen turns a
    /// failure into the state its status line reports, a background refresh
    /// logs it and keeps what it already holds.
    private static void Take((string body, string error) answer, bool ownedByAScreen) {
        // the master switch means the mod does nothing, and that has to cover an
        // answer to a question asked before it was thrown: taking this one in
        // would write RemoteBests and announce itself while the mod is inert
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
            // logged because this is the one guard here that nothing is known
            // to trigger. Silent, it can neither be tested nor caught doing its
            // job in the wild; the line is how the path stops being a theory
            Logger.Log(LogLevel.Warn, LogTag, "the level was replaced under the export screen; closed it");
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

        // Hotkeys holds the combo at rest behind the pause menu, but not behind
        // this screen's own pause: one press opens, the next closes
        if (Hotkeys.OpenExportMenu.Pressed) {
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

        List<PendingUpdate> updates = ExportSource.Collect(level.Session);
        Logger.Log(LogLevel.Info, LogTag,
            $"export: scope={SegmentAutoDetect.ScopeOf(level.Session)}"
            + $" rows={updates.Count} held={SessionBests.Describe()}");
        // nothing run this session, or a run whose segment does not map to a
        // row: either way there is nothing to export and no screen to show
        if (updates.Count == 0) {
            PopupMessageUtils.Show(Dialog.Clean("SRS_EXPORT_NOTHING"), null);
            return;
        }

        pausedBeforeOpen = level.Paused;
        openLevel = level;
        level.Paused = true;
        // taken by every open, whichever branch follows: a POST from the
        // previous screen may still be in flight, and its continuation checks
        // this to know its screen is gone
        int fetch = ++generation;

        // a refresh has answered: the table is built now, with no wait at all,
        // and another starts behind it so the next open is no staler than this
        // one. What is on screen can be a refresh old, and the write is what
        // guards against that -- it compares each cell before touching it
        if (RemoteBests.IsResolved) {
            Build(level, updates);
            // and only then a refresh behind it. Opening, closing and opening
            // again inside a minute is one action rather than three, and asking
            // three times gets the same answer three times -- at a call against
            // the player's own script each
            if (RemoteBests.Age > AskAgainAfter) {
                Refresh("a screen opened on data already held");
            }

            return;
        }

        // nothing held: the first open of a session that launched offline, or
        // one whose refresh has not landed yet
        RemoteBests.BeginFetch();
        string url = SrsModule.Settings.ExportUrl;
        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            if (fetch != generation) {
                // the screen this belonged to is gone and another has taken its
                // place. Logged for the same reason the level-replaced guard is:
                // silent, there is no way to see it work, and this one guards
                // against an older answer overwriting a newer one
                Logger.Log(LogLevel.Info, LogTag, "a fetch resolved after its screen was replaced; discarded");
                return;
            }

            // the generation only moves when a screen opens, and the sheet is
            // repointed or forgotten from Mod Options with no screen up at all:
            // closing this one and forgetting the URL leaves the generation
            // where it was, and this answer would resolve RemoteBests against a
            // sheet nobody is pointed at any more. Nothing rebuilds either way,
            // the screen is gone by construction (Mod Options is behind the
            // pause, and OnPause closes this screen)
            if (url != SrsModule.Settings.ExportUrl) {
                Logger.Log(LogLevel.Info, LogTag,
                    "a fetch resolved for a sheet URL that is no longer the one set; discarded");
                return;
            }

            try {
                Take(task.Result, ownedByAScreen: true);
            } catch (Exception e) {
                // a screen is waiting on this: with no state to show, it would
                // sit on "loading" until the player cancels it
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

        // stale flags from this session must never bleed into a later one:
        // if a background fetch/submit resolves after Close(), its queued
        // rebuild/summary would otherwise fire against a freshly reopened menu
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

    /// Puts a screen up in place of whatever is there. Every screen this class
    /// shows goes through here: the three ways out of a menu are wired in one
    /// place rather than repeated, which is what stops a new screen from
    /// forgetting one and trapping the player in a paused level.
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
        ExportColumns columns = ExportColumns.Measure(updates, level.Session);
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

            newMenu.Add(new UpdateRow(updates, i, columns, odd, level.Session));
            odd = !odd;
        }

        newMenu.Add(new TableFooter(columns));

        TextMenu.Button exportButton = new(ExportLabel(updates)) { Disabled = !RemoteBests.IsResolved };
        exportButton.OnUpdate = () => {
            exportButton.Label = ExportLabel(updates);
            exportButton.Disabled = !RemoteBests.IsResolved;
        };
        exportButton.Pressed(() => Submit(level, updates));
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
                // the raw cell, so the script compares what the sheet displays
                // against itself: a reformat of it would differ on every row
                // the sheet writes short ("1:36.9")
                Expect = u.RemoteCell,
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
            // the write emptied the script's own cache, so the next read costs
            // full price: pay it now rather than at the next open
            Refresh("an export was just written");
        });
    }

    private static void QueueSummary(List<string> lines) {
        summaryLines = lines;
        queuedSummary = true;
    }

    /// Up while the first fetch is in flight, in place of the table. Rows built
    /// before the sheet has answered compare against values that have not
    /// arrived: every one of them pre-ticks as an improvement, which reads as a
    /// finished table and is not one. Waiting is what makes the table mean
    /// something, and it is why nothing has to be patched up afterwards.
    private static void ShowLoading(Level level) {
        TextMenu newMenu = new();
        newMenu.Add(new TextMenu.Header(Dialog.Clean("SRS_EXPORT_TITLE")));
        newMenu.Add(new TextMenu.SubHeader(Dialog.Clean("SRS_EXPORT_LOADING")));

        // a way out that does not require knowing that Back closes it: this is
        // the one screen the player may want to leave before it has done anything
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

        Show(level, newMenu);
    }
}
