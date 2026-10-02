using Celeste.Mod.CelesteHotkeys;

namespace Celeste.Mod.SpeedrunSheet;

// the four rebindable hotkeys, as a CelesteHotkeys table polled once per
// frame. The module reads a binding as a combo, which Everest's key config
// screen cannot express: every binding is [SettingIgnore] and bound from the
// module's own screen (ModMenu)
public static class Hotkeys {
    internal static readonly Keybind<SrsSettings> CycleCategory =
        new("MODOPTIONS_SRS_CYCLECATEGORY", nameof(SrsSettings.CycleCategory));
    internal static readonly Keybind<SrsSettings> ToggleShowTier =
        new("MODOPTIONS_SRS_TOGGLESHOWTIER", nameof(SrsSettings.ToggleShowTier));
    internal static readonly Keybind<SrsSettings> ToggleShowSelection =
        new("MODOPTIONS_SRS_TOGGLESHOWSELECTION", nameof(SrsSettings.ToggleShowSelection));
    internal static readonly Keybind<SrsSettings> OpenExportMenu =
        new("MODOPTIONS_SRS_OPENEXPORTMENU", nameof(SrsSettings.OpenExportMenu));

    internal static readonly Keybind<SrsSettings>[] All =
        [CycleCategory, ToggleShowTier, ToggleShowSelection, OpenExportMenu];

    internal static readonly HotkeySet<SrsSettings> Set = new(() => SrsModule.Settings, All);

    internal static readonly KeybindScreenText Text = new() {
        HeaderId = "SRS_KEYBINDS",
        ComboHintId = "SRS_KEYBIND_COMBO_HINT",
        ClearHintId = "SRS_KEYBIND_CLEAR_HINT",
        TimeoutFormatId = "SRS_KEYBIND_TIMEOUT",
    };

    private static bool levelPaused;

    public static void Load() {
        // Keys.None out of what is already on disk: FNA reports it held for
        // every key absent from its SDL -> XNA table, and Everest's own rebind
        // screen, where these used to be set, records it unfiltered
        Bindable.Sanitize(SrsModule.Settings);

        // loaded first, so this hook is the innermost one: after orig the
        // hotkeys are updated before RunWatcher, TierComparison,
        // SegmentAutoDetect and ExportMenu read them on the same frame
        On.Celeste.Level.Update += LevelOnUpdate;
    }

    public static void Unload() {
        On.Celeste.Level.Update -= LevelOnUpdate;
    }

    /// Whether the hotkey fired this frame. While the level is paused only the
    /// export screen's own hotkey answers, and only behind the pause that
    /// screen holds, or the combo that opened it could not close it. The other
    /// three move the selection, which is what the open screen is a view of.
    /// Polled through the pause rather than skipped, so a combo held across it
    /// does not fire when it ends.
    internal static bool Pressed(Keybind<SrsSettings> keybind) =>
        Set.Pressed(keybind)
        && (!levelPaused || (keybind == OpenExportMenu && ExportMenu.HoldsThePause));

    private static void LevelOnUpdate(On.Celeste.Level.orig_Update orig, Level self) {
        orig(self);

        levelPaused = self.Paused;
        // the module counts the master switch, the debug console, an
        // unfocused window and any open remap screen as a pause
        Set.Update(SrsModule.Settings.Enabled);
    }
}
