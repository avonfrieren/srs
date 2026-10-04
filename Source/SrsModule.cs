using System;
using System.Linq;
using System.Reflection;
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
        // before anything formats a time
        AdoptSpeedrunToolsTimeFormat();

        SheetImporter.Load();
        // before ExportMenu.Load, which asks the sheet at launch
        ExportTarget.Load();
        // Level.Update hook order matters: each later Load wraps the previous
        // hooks, so after orig the frame runs innermost-first — Hotkeys reads
        // the frame's input before anything consumes it, RunWatcher feeds
        // the tracker, TierComparison computes the tier from its latest record,
        // and ExportMenu reads its hotkey last
        Hotkeys.Load();
        RunWatcher.Load();
        TierComparison.Load();
        // last: it reads Hotkeys on the frame Hotkeys updated it, and only reads
        // what the others produced
        ExportMenu.Load();
        // no Level.Update hook of its own: an Everest enter event, so it is
        // outside the ordering the comment above describes
        SessionBests.Load();
    }

    public override void Unload() {
        SessionBests.Unload();
        ExportMenu.Unload();
        TierComparison.Unload();
        RunWatcher.Unload();
        Hotkeys.Unload();
        SheetImporter.Unload();
    }

    /// srs prints its times in Speed Run Tool's format, so it takes the
    /// formatter rather than copying it. By reflection, the only way in:
    /// RoomTimerData.FormatTime is public, its class is not, the ModInterop
    /// does not export it, and a Publicizer is ruled out.
    ///
    /// isPbTime false: true returns "" for a zero, Speed Run Tool's absent PB.
    private static void AdoptSpeedrunToolsTimeFormat() {
        try {
            Assembly assembly = Everest.Modules
                .FirstOrDefault(module => module.Metadata?.Name == "SpeedrunTool")?
                .GetType().Assembly;
            MethodInfo method = assembly?
                .GetType("Celeste.Mod.SpeedrunTool.RoomTimer.RoomTimerData")?
                .GetMethod("FormatTime", BindingFlags.Public | BindingFlags.Static,
                    null, [typeof(long), typeof(bool)], null);

            if (method?.CreateDelegate(typeof(Func<long, bool, string>)) is Func<long, bool, string> format) {
                TimeFormat.Format = ticks => format(ticks, false);
                return;
            }
        } catch (Exception e) {
            Logger.Log(LogLevel.Error, "srs", "could not reach SpeedrunTool's time format: " + e);
        }

        // fatal on purpose: a second formatter that agrees today drifts in
        // silence, and writes a time into the player's sheet that no longer
        // matches the timer. Failing loudly costs a release instead
        throw new InvalidOperationException(
            "SpeedrunTool's RoomTimerData.FormatTime could not be found. It is where srs takes its"
            + " time format from, and srs keeps no copy. This needs a srs update.");
    }

    public override void LoadSettings() {
        base.LoadSettings();

        // a stored URL beats a new default (SheetUrls): without this, a player
        // who ever saved stays on the frozen workbook
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
        // a save that fails leaves the migration in memory, and it runs again
        // next launch
        TrySaveSettings("the migrated sheet urls");
    }

    /// Everest catches the write itself, but not the File.Delete and
    /// CreateDirectory it does first: every save srs asks for goes through
    /// here, or a locked or read-only Saves folder crashes the game from a menu
    /// press or a hook. What was changed still holds in memory.
    internal static void TrySaveSettings(string what) {
        try {
            Instance.SaveSettings();
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, "srs", $"could not save the settings ({what}): {e.GetType().Name}");
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
