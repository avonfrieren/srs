using System;
using System.Collections.Generic;
using System.Globalization;
using Monocle;
using SDL2;

namespace Celeste.Mod.SpeedrunSheet;

// Mod Options entries for the export target. The URL is the credential
// (ExportTarget): read from the clipboard, never rendered back.
public static class ExportUrlMenu {
    private const string LogTag = "srs";

    // condition behind the Visible of the entries returned by CreateMenuEntries
    internal static bool HasUrl => ExportTarget.IsSet;

    // written from the ping's continuation (a thread-pool thread) and read on
    // the game thread in setButton.OnUpdate, which is the only place either is
    // turned into what the screen shows
    private static volatile string message;
    private static volatile bool checking;

    // nothing cancels a ping in flight: a second paste while the first is
    // still out would otherwise let the older answer overwrite the newer
    private static volatile int generation;

    public static List<TextMenu.Item> CreateMenuEntries(TextMenu menu) {
        SrsSettings settings = SrsModule.Settings;

        // a check's answer belongs to the visit it was asked in: the menu is
        // rebuilt on every open, and the next one goes back to the detail line.
        // One still out lands on this visit instead
        if (!checking) {
            message = null;
        }

        // declared up front (unset) so each Pressed() closure below can
        // reference the others' Label/Visible/Title after a change
        TextMenu.Button setButton = new(StatusLabel(settings));
        TextMenu.SubHeader status = new(DetailLine(settings), topPadding: false) {
            Visible = HasUrl,
        };
        TextMenu.Button forgetButton = new(Dialog.Clean("SRS_EXPORT_URL_FORGET")) {
            Visible = HasUrl,
        };

        // the only place `message` and `checking` are read. ⚠️ TextMenu calls
        // OnUpdate on every item each frame, visible or not, so this owns
        // `status.Visible`: the master switch must be read here too, or what it
        // sets is undone next frame
        setButton.OnUpdate = () => {
            if (!SrsModule.Settings.Enabled) {
                return;
            }

            string shown = checking ? Dialog.Clean("SRS_EXPORT_URL_CHECKING") : message;
            status.Title = shown ?? DetailLine(settings);
            status.Visible = shown != null || HasUrl;
        };

        setButton.Pressed(() => {
            string pasted = ReadClipboard();
            if (string.IsNullOrWhiteSpace(pasted)) {
                message = Dialog.Clean("SRS_EXPORT_URL_CLIPBOARD_EMPTY");
                return;
            }

            pasted = pasted.Trim();
            if (!ExportProtocol.IsEndpointUrl(pasted)) {
                // never echo what was on the clipboard: it may be the URL of
                // someone else's sheet, and it may be anything at all
                message = Dialog.Clean("SRS_EXPORT_URL_CLIPBOARD_INVALID");
                return;
            }

            if (!ExportTarget.Set(pasted)) {
                // a check still out for an earlier paste would answer over this line
                generation++;
                checking = false;
                message = Dialog.Clean("SRS_EXPORT_URL_SAVE_FAILED");
                return;
            }

            settings.ExportUrlSetOn = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            RemoteBests.Reset();
            SrsModule.TrySaveSettings("the date the sheet URL was set");

            setButton.Label = StatusLabel(settings);
            forgetButton.Visible = true;

            BeginCheck(pasted);
            // the check is about to warm the script's own cache, so the export
            // screen may as well open on data rather than on a loading line
            ExportMenu.Refresh("a sheet URL was just set");
        });

        // press once to arm ("Forget Sheet URL?"), press again to clear:
        // TextMenu.Button has no confirm dialog, so the two-step is one
        bool forgetArmed = false;
        forgetButton.OnLeave = () => {
            forgetArmed = false;
            forgetButton.Label = Dialog.Clean("SRS_EXPORT_URL_FORGET");
        };
        forgetButton.Pressed(() => {
            if (!forgetArmed) {
                forgetArmed = true;
                forgetButton.Label = $"{Dialog.Clean("SRS_EXPORT_URL_FORGET")}?";
                return;
            }

            if (!ExportTarget.Forget()) {
                forgetArmed = false;
                forgetButton.Label = Dialog.Clean("SRS_EXPORT_URL_FORGET");
                generation++;
                checking = false;
                message = Dialog.Clean("SRS_EXPORT_URL_FORGET_FAILED");
                return;
            }

            settings.ExportUrlSetOn = "";
            RemoteBests.Reset();
            SrsModule.TrySaveSettings("the sheet URL forgotten");

            forgetArmed = false;
            generation++;
            message = null;
            checking = false;
            forgetButton.Label = Dialog.Clean("SRS_EXPORT_URL_FORGET");
            forgetButton.Visible = false;
            setButton.Label = StatusLabel(settings);
        });

        menu.Add(setButton);
        menu.Add(status);
        menu.Add(forgetButton);

        return new List<TextMenu.Item> { forgetButton };
    }

    /// Asks the endpoint for its rows. The wrong deployment and an unauthorised
    /// one both answer 200 with something that is not ours, so the check is that
    /// it parses rather than that it responded.
    private static void BeginCheck(string url) {
        int fetch = ++generation;
        checking = true;
        message = null;

        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            if (fetch != generation) {
                return;
            }

            // nothing above this continuation observes a throw: the status
            // would stay on "Asking the sheet..." for the rest of the visit
            try {
                (string body, string error) = task.Result;
                if (error != null) {
                    message = $"{Dialog.Clean("SRS_EXPORT_URL_CHECK_FAILED")} {error}";
                } else if (ExportProtocol.TryParseRows(body, out List<RemoteRow> rows, out string _)) {
                    // the count proves the export script answered with real rows;
                    // the player has no use for the number, the log keeps it
                    Logger.Log(LogLevel.Info, LogTag, $"sheet URL check: {rows.Count} rows read");
                    message = Dialog.Clean("SRS_EXPORT_URL_CHECK_OK");
                } else {
                    message = Dialog.Clean("SRS_EXPORT_URL_CHECK_NOT_SHEET");
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, LogTag, "a sheet URL check could not be read: " + e.GetType().Name);
                message = Dialog.Clean("SRS_EXPORT_URL_CHECK_NOT_SHEET");
            } finally {
                checking = false;
            }
        });
    }

    /// SDL owns the clipboard; FNA exposes it and nothing in Celeste wraps it.
    /// Never throws: a clipboard that cannot be read is an empty one.
    private static string ReadClipboard() {
        try {
            return SDL.SDL_GetClipboardText();
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, "clipboard unreadable: " + e.Message);
            return null;
        }
    }

    // the label is the action, not the state: the state is the line below it,
    // which only appears once there is one
    private static string StatusLabel(SrsSettings settings) =>
        Dialog.Clean(!ExportTarget.IsSet
            ? "SRS_EXPORT_URL_FROM_CLIPBOARD"
            : "SRS_EXPORT_URL_REPLACE_FROM_CLIPBOARD");

    // never includes the URL itself — just enough to reassure the player
    // something is configured, and when
    private static string DetailLine(SrsSettings settings) =>
        $"{Dialog.Clean("SRS_EXPORT_URL_DETAIL")} {settings.ExportUrlSetOn}";
}
