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
        // hooks, so after orig the frame runs innermost-first — Hotkeys reads
        // the frame's input before anything consumes it, RunWatcher captures
        // the finished run, TierComparison computes the tier from it,
        // SegmentAutoDetect moves the selection last (suspended while a
        // completed run's tier is shown)
        Hotkeys.Load();
        RunWatcher.Load();
        TierComparison.Load();
        SegmentAutoDetect.Load();
        // last: like every hook above it, this one reads Hotkeys on the frame
        // Hotkeys updated it, so it must stay outside Hotkeys' hook. Nothing
        // else constrains it, it only reads what the others produced
        ExportMenu.Load();
    }

    public override void Unload() {
        ExportMenu.Unload();
        SegmentAutoDetect.Unload();
        TierComparison.Unload();
        RunWatcher.Unload();
        Hotkeys.Unload();
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

        // the tab URLs are stored settings, so the defaults above reach nobody
        // who has ever saved: without this, every existing player stays on the
        // workbook frozen on 2026-08-28, which still answers and silently stops
        // receiving retimings
        MigrateSheetUrls();
    }

    // substitutes the frozen spreadsheet id in the three stored URLs and saves
    // if any of them carried it. Idempotent: a save that fails changes nothing
    // but the file on disk, and the next launch migrates again
    private void MigrateSheetUrls() {
        SrsSettings settings = Settings;
        bool changed = false;

        if (SheetUrls.Migrate(settings.ASidesUrl) is string aSides) {
            settings.ASidesUrl = aSides;
            changed = true;
        }

        if (SheetUrls.Migrate(settings.BSidesUrl) is string bSides) {
            settings.BSidesUrl = bSides;
            changed = true;
        }

        if (SheetUrls.Migrate(settings.FarewellUrl) is string farewell) {
            settings.FarewellUrl = farewell;
            changed = true;
        }

        if (!changed) {
            return;
        }

        Logger.Log(LogLevel.Info, "srs", "Repointed the stored sheet urls at the current reference workbook");
        try {
            SaveSettings();
        } catch (Exception e) {
            // Everest catches the write itself, but not the File.Delete and
            // CreateDirectory it does first: those throw out of LoadSettings,
            // which would take the whole mod down over a settings file. The
            // migration already holds in memory, and runs again next launch
            Logger.Log(LogLevel.Warn, "srs", $"Could not persist the migrated sheet urls: {e}");
        }
    }

    // only the header comes from base: every entry of the section is built by
    // hand in ModMenu, since the master switch has to be able to hide them all.
    // The header must still come first — entries added before it would land in
    // the previous mod's section
    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
        CreateModMenuSectionHeader(menu, inGame, snapshot);
        ModMenu.CreateMenu(menu, inGame);
    }
}
