using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// the four rebindable hotkeys, updated once per frame from a single input
// snapshot. Bound through KeybindConfigUi rather than Everest's key config
// screen: the bindings are combos here, and Everest's screen has no way to
// say so
public static class Hotkeys {
    internal static ComboHotkey CycleCategory { get; private set; }
    internal static ComboHotkey ToggleShowTier { get; private set; }
    internal static ComboHotkey ToggleShowSelection { get; private set; }
    internal static ComboHotkey OpenExportMenu { get; private set; }

    private static ComboHotkey[] all = [];

    public static void Load() {
        SrsSettings settings = SrsModule.Settings;

        // Keys.None must not survive into a hotkey. FNA hands it back for a key
        // absent from its SDL -> XNA table, then reports it held like a real
        // key, so a binding carrying it fires on every unmappable key of the
        // layout. Everest filters it out of [DefaultButtonBinding], but not out
        // of its own rebind screen (vanilla KeyboardConfigUI.AddRemap takes the
        // pressed key unfiltered) — and that screen is where srs's bindings
        // used to be set, so strip what is already on disk rather than trust it
        foreach (ButtonBinding binding in Bindings(settings)) {
            binding.Keys.RemoveAll(key => key == Keys.None);
        }

        CycleCategory = new ComboHotkey(settings.CycleCategory);
        ToggleShowTier = new ComboHotkey(settings.ToggleShowTier);
        ToggleShowSelection = new ComboHotkey(settings.ToggleShowSelection);
        OpenExportMenu = new ComboHotkey(settings.OpenExportMenu);
        all = [CycleCategory, ToggleShowTier, ToggleShowSelection, OpenExportMenu];

        // loaded first, so this hook is the innermost one: after orig the
        // hotkeys are updated before RunWatcher, TierComparison,
        // SegmentAutoDetect and ExportMenu read them on the same frame
        On.Celeste.Level.Update += LevelOnUpdate;
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
        all = [];
    }

    internal static IEnumerable<ButtonBinding> Bindings(SrsSettings settings) {
        yield return settings.CycleCategory;
        yield return settings.ToggleShowTier;
        yield return settings.ToggleShowSelection;
        yield return settings.OpenExportMenu;
    }

    // marks whatever is held right now as already consumed. Called when the mod
    // is switched back on and after a rebind: the key that was just bound is
    // still down when the screen hands focus back, and without this it would
    // fire the hotkey it was bound to on that very frame
    internal static void Resync() {
        InputSnapshot input = InputSnapshot.Current();
        foreach (ComboHotkey hotkey in all) {
            hotkey.Resync(input);
        }
    }

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        // held at rest instead of simply skipped: a combo held across a pause,
        // a console session or a disabled stretch would otherwise be a rising
        // edge on the frame that stretch ends. The pause case covers Mod
        // Options and KeybindConfigUi, which are both reached through it
        if (!SrsModule.Settings.Enabled || self.Paused || Engine.Commands.Open) {
            Resync();
            return;
        }

        InputSnapshot input = InputSnapshot.Current();
        foreach (ComboHotkey hotkey in all) {
            hotkey.Update(input);
        }
    }
}
