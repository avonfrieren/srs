using System;
using System.Collections.Generic;
using Monocle;
using SDL2;

namespace Celeste.Mod.SpeedrunSheet;

// Mod Options entries for the export target (ExportUrl). The URL is a bearer
// credential (no auth on the Apps Script Web App side), so it must never be
// shown, logged, or pre-filled: it is read from the clipboard and never
// rendered back.
public static class ExportUrlMenu {
    private const string LogTag = "srs";

    // condition behind the Visible of the entries returned by CreateMenuEntries
    internal static bool HasUrl => !string.IsNullOrEmpty(SrsModule.Settings.ExportUrl);

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

        // declared up front (unset) so each Pressed() closure below can
        // reference the others' Label/Visible/Title after a change
        TextMenu.Button setButton = new(StatusLabel(settings));
        TextMenu.SubHeader status = new(DetailLine(settings), topPadding: false) {
            Visible = HasUrl,
        };
        TextMenu.Button forgetButton = new(Dialog.Clean("SRS_EXPORT_URL_FORGET")) {
            Visible = HasUrl,
        };

        // the ping answers on another thread; this is where its answer becomes
        // visible, and the only place `message` and `checking` are read
        setButton.OnUpdate = () => {
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

            settings.ExportUrl = pasted;
            settings.ExportUrlSetOn = DateTime.Now.ToString("yyyy-MM-dd");
            RemoteBests.Reset();
            SrsModule.Instance.SaveSettings();

            setButton.Label = StatusLabel(settings);
            forgetButton.Visible = true;

            BeginCheck(pasted);
            // the check is about to warm the script's own cache, so the export
            // screen may as well open on data rather than on a loading line
            ExportMenu.Refresh("a sheet URL was just set");
        });

        // press once to arm ("Forget Sheet URL?"), press again to actually
        // clear — no built-in confirm dialog for TextMenu.Button in this
        // codebase, so this two-step is the confirmation
        bool forgetArmed = false;
        forgetButton.Pressed(() => {
            if (!forgetArmed) {
                forgetArmed = true;
                forgetButton.Label = $"{Dialog.Clean("SRS_EXPORT_URL_FORGET")}?";
                return;
            }

            settings.ExportUrl = "";
            settings.ExportUrlSetOn = "";
            RemoteBests.Reset();
            SrsModule.Instance.SaveSettings();

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

    /// Asks the endpoint for its rows. A URL of the right shape can still be
    /// the wrong deployment, or one nobody has authorised, and both answer 200
    /// with something that is not ours — so the check is that it parses, not
    /// that it responded.
    private static void BeginCheck(string url) {
        int fetch = ++generation;
        checking = true;
        message = null;

        _ = ExportClient.FetchAsync(url).ContinueWith(task => {
            if (fetch != generation) {
                return;
            }

            (string body, string error) = task.Result;
            message = error != null
                ? $"{Dialog.Clean("SRS_EXPORT_URL_CHECK_FAILED")} {error}"
                : ExportProtocol.TryParseRows(body, out List<RemoteRow> rows, out string _)
                    ? $"{Dialog.Clean("SRS_EXPORT_URL_CHECK_OK")} {rows.Count}"
                    : Dialog.Clean("SRS_EXPORT_URL_CHECK_NOT_SHEET");
            checking = false;
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
        Dialog.Clean(string.IsNullOrEmpty(settings.ExportUrl)
            ? "SRS_EXPORT_URL_FROM_CLIPBOARD"
            : "SRS_EXPORT_URL_REPLACE_FROM_CLIPBOARD");

    // never includes the URL itself — just enough to reassure the player
    // something is configured, and when
    private static string DetailLine(SrsSettings settings) =>
        $"{Dialog.Clean("SRS_EXPORT_URL_DETAIL")} {settings.ExportUrlSetOn}";
}
