using System;
using FMOD.Studio;

namespace Celeste.Mod.SpeedrunSheet;

public class SrsModule : EverestModule {
    public static SrsModule Instance { get; private set; }

    public override Type SettingsType => typeof(SrsSettings);
    public static SrsSettings Settings => (SrsSettings)Instance._Settings;

    public SrsModule() {
        Instance = this;
    }

    public override void Load() {
        SheetImporter.Load();
        TierComparison.Load();
    }

    public override void Unload() {
        TierComparison.Unload();
        SheetImporter.Unload();
    }

    public override void LoadSettings() {
        base.LoadSettings();
        // v0.1.0 pointed at the IL tab (whole chapters); checkpoint selection
        // needs the CP tab, so move unchanged settings to the new default
        if (Settings.SheetUrl == SrsSettings.LegacySheetUrl) {
            Settings.SheetUrl = SrsSettings.DefaultSheetUrl;
        }
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
        base.CreateModMenuSection(menu, inGame, snapshot);
        SegmentSelector.CreateMenuEntries(menu);
        SheetImporter.CreateMenuEntries(menu);
    }
}
