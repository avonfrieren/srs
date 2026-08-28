using System.Collections.Generic;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// the whole Mod Options section, built by hand instead of letting Everest
// generate the toggles: the master switch has to hide every other entry, which
// means holding them all in one list. Nothing is auto-generated any more, so
// the key bindings Everest would append are gone too — they are combos now,
// and KeybindConfigUi owns them
internal static class ModMenu {
    internal static void CreateMenu(TextMenu menu, bool inGame) {
        SrsSettings settings = SrsModule.Settings;

        TextMenu.OnOff enabled = new(Dialog.Clean("MODOPTIONS_SRS_ENABLED"), settings.Enabled);
        menu.Add(enabled);

        // everything added below the master switch is one of its sub-options
        int first = menu.Items.Count;

        TextMenu.OnOff showTier = new(Dialog.Clean("MODOPTIONS_SRS_SHOWTIER"), settings.ShowTier);
        showTier.Change(on => settings.ShowTier = on);
        menu.Add(showTier);

        TextMenu.OnOff showSelection = new(Dialog.Clean("MODOPTIONS_SRS_SHOWSELECTION"), settings.ShowSelection);
        showSelection.Change(on => settings.ShowSelection = on);
        menu.Add(showSelection);

        SegmentSelector.CreateMenuEntries(menu);
        SheetImporter.CreateMenuEntries(menu);

        // ExportUrlMenu keeps these two hidden until an export URL is set
        List<TextMenu.Item> urlDependent = ExportUrlMenu.CreateMenuEntries(menu);

        // in game only: the export screen needs a level, and it saves binding a
        // hotkey just to reach it
        if (inGame) {
            TextMenu.Button openExport = new(Dialog.Clean("MODOPTIONS_SRS_OPENEXPORTMENU"));
            openExport.Pressed(() => {
                if (Engine.Scene is not Level level) {
                    return;
                }

                // Unpause tears the pause menu down properly — closing coroutine,
                // settings save, unpause sound. Opening on the next frame lets it
                // finish, and leaves ExportMenu recording an unpaused level, so
                // closing the export screen returns to the game rather than to a
                // pause with no menu in it
                level.Unpause();
                Engine.Scene.OnEndOfFrame += () => ExportMenu.Open(level);
            });
            menu.Add(openExport);
        }

        TextMenu.Button keybinds = new(Dialog.Clean("SRS_KEYBINDS"));
        keybinds.Pressed(() => {
            menu.Focused = false;
            KeybindConfigUi ui = new() { OnClose = () => menu.Focused = true };
            Engine.Scene.Add(ui);
            Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
        });
        menu.Add(keybinds);

        // taken as a range rather than listed entry by entry: SegmentSelector
        // adds nothing at all when no sheet data is loaded, so the list cannot
        // be written out by hand without going out of step with what is there
        List<TextMenu.Item> subOptions = menu.Items.GetRange(first, menu.Items.Count - first);

        enabled.Change(on => {
            settings.Enabled = on;
            ShowSubOptions(subOptions, urlDependent, on);
            if (on) {
                // the startup refresh is skipped while the mod is off, so this
                // is the first chance to pick up a sheet retimed in the meantime
                SheetImporter.BeginUpdate(null);
            }
        });

        ShowSubOptions(subOptions, urlDependent, settings.Enabled);
    }

    // the master switch hides everything, but turning the mod back on must not
    // reveal entries their own owner decided to keep hidden
    private static void ShowSubOptions(List<TextMenu.Item> subOptions, List<TextMenu.Item> urlDependent, bool on) {
        SetVisible(subOptions, on);
        SetVisible(urlDependent, on && ExportUrlMenu.HasUrl);
    }

    private static void SetVisible(List<TextMenu.Item> items, bool visible) {
        foreach (TextMenu.Item item in items) {
            item.Visible = visible;
        }
    }
}
