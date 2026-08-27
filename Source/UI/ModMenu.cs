using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// the whole Mod Options section, built by hand instead of letting Everest
// generate the toggles: the master switch has to hide every other entry, and an
// auto-generated entry cannot be reached to be hidden
internal static class ModMenu {
    internal static void CreateMenu(TextMenu menu) {
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

        // taken as a range rather than listed entry by entry: SegmentSelector
        // adds nothing at all when no sheet data is loaded, so the list cannot
        // be written out by hand without going out of step with what is there
        List<TextMenu.Item> subOptions = menu.Items.GetRange(first, menu.Items.Count - first);

        enabled.Change(on => {
            settings.Enabled = on;
            SetVisible(subOptions, on);
            if (on) {
                // the startup refresh is skipped while the mod is off, so this
                // is the first chance to pick up a sheet retimed in the meantime
                SheetImporter.BeginUpdate(null);
            }
        });

        SetVisible(subOptions, settings.Enabled);
    }

    private static void SetVisible(List<TextMenu.Item> items, bool visible) {
        foreach (TextMenu.Item item in items) {
            item.Visible = visible;
        }
    }
}
