using System;
using System.IO;
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
        // Level.Update hook order matters: each later Load wraps the previous
        // hooks, so after orig the frame runs innermost-first — RunWatcher
        // captures the finished run, TierComparison computes the tier from it,
        // SegmentAutoDetect moves the selection last (suspended while a
        // completed run's tier is shown)
        RunWatcher.Load();
        TierComparison.Load();
        SegmentAutoDetect.Load();
    }

    public override void Unload() {
        SegmentAutoDetect.Unload();
        TierComparison.Unload();
        RunWatcher.Unload();
        SheetImporter.Unload();
    }

    public override void LoadSettings() {
        // mod name changed from "srs" to "Speedrun Sheet" in v1.0.0; if the new
        // settings file doesn't exist but the old one does, load from the old path
        var oldPath = Path.Combine(Everest.PathSettings, "modsettings-srs.celeste");
        var newPath = Path.Combine(Everest.PathSettings, $"modsettings-{Metadata.Name}.celeste");
        if (!File.Exists(newPath) && File.Exists(oldPath)) {
            // copy the old file to the new location before loading, so base.LoadSettings reads it
            File.Copy(oldPath, newPath);
        }

        base.LoadSettings();
    }

    // only the header comes from base: every entry of the section is built by
    // hand in ModMenu, since the master switch has to be able to hide them all.
    // The header must still come first — entries added before it would land in
    // the previous mod's section
    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
        CreateModMenuSectionHeader(menu, inGame, snapshot);
        ModMenu.CreateMenu(menu);
        // last, and outside what the master switch hides: this is still how the
        // three hotkeys are bound
        CreateModMenuSectionKeyBindings(menu, inGame, snapshot);
    }
}
