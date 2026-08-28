using System;
using System.Collections.Generic;
using Celeste.Mod.UI;

namespace Celeste.Mod.SpeedrunSheet;

// Mod Options entries for the export target (ExportUrl). The URL is a bearer
// credential (no auth on the Apps Script Web App side), so it must never be
// shown, logged, or pre-filled: OuiModOptionString always opens on an empty
// field, and submitting empty means "no change" (never "clear the URL" — use
// the dedicated Forget button for that).
public static class ExportUrlMenu {
    // condition behind the Visible of the entries returned by CreateMenuEntries
    internal static bool HasUrl => !string.IsNullOrEmpty(SrsModule.Settings.ExportUrl);

    // OuiModOptionString is an Overworld Oui, reachable only from the title
    // screen's Mod Options — in a paused level, Goto<OuiModOptionString> would
    // have nowhere sensible to return to, so the entry point button is greyed
    // out (TextMenu.Item.Disabled) like SegmentSelector's sliders while
    // auto-detect owns them.
    // returns the entries whose Visible this class owns (shown only once a URL
    // is set), so the caller's own show/hide can leave them alone
    public static List<TextMenu.Item> CreateMenuEntries(TextMenu menu, bool inGame) {
        SrsSettings settings = SrsModule.Settings;

        // declared up front (unset) so each Pressed() closure below can
        // reference the others' Label/Visible/Title after a change
        TextMenu.Button setButton = new(StatusLabel(settings)) { Disabled = inGame };
        TextMenu.SubHeader status = new(DetailLine(settings), topPadding: false) {
            Visible = HasUrl,
        };
        TextMenu.Button forgetButton = new(Dialog.Clean("SRS_EXPORT_URL_FORGET")) {
            Visible = HasUrl,
        };

        setButton.Pressed(() => {
            if (inGame) {
                return;
            }

            // starting value is always "" — the current URL is never shown,
            // not even to prefill the field for editing
            string pending = "";
            menu.SceneAs<Overworld>().Goto<OuiModOptionString>().Init<OuiModOptions>(
                "",
                v => pending = v,
                confirmed => {
                    // empty submission (including a plain cancel) changes
                    // nothing — only a non-empty confirmed value updates the URL
                    if (!confirmed || string.IsNullOrEmpty(pending)) {
                        return;
                    }

                    settings.ExportUrl = pending;
                    settings.ExportUrlSetOn = DateTime.Now.ToString("yyyy-MM-dd");
                    RemoteBests.Reset();
                    SrsModule.Instance.SaveSettings();

                    setButton.Label = StatusLabel(settings);
                    status.Title = DetailLine(settings);
                    status.Visible = true;
                    forgetButton.Visible = true;
                },
                500, 0);
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
            forgetButton.Label = Dialog.Clean("SRS_EXPORT_URL_FORGET");
            forgetButton.Visible = false;
            setButton.Label = StatusLabel(settings);
            status.Visible = false;
        });

        menu.Add(setButton);
        menu.Add(status);
        if (inGame) {
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("SRS_EXPORT_URL_TITLE_ONLY"), topPadding: false));
        }
        menu.Add(forgetButton);

        return new List<TextMenu.Item> { status, forgetButton };
    }

    private static string StatusLabel(SrsSettings settings) =>
        $"{Dialog.Clean("SRS_EXPORT_URL")}   " +
        Dialog.Clean(string.IsNullOrEmpty(settings.ExportUrl) ? "SRS_EXPORT_URL_UNSET" : "SRS_EXPORT_URL_SET");

    // never includes the URL itself — just enough to reassure the player
    // something is configured, and when
    private static string DetailLine(SrsSettings settings) =>
        $"{Dialog.Clean("SRS_EXPORT_URL_DETAIL")} {settings.ExportUrlSetOn}";
}
