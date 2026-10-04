namespace Celeste.Mod.SpeedrunSheet;

public class SrsSettings : EverestModuleSettings {
    // master switch. Off, the mod is inert — no HUD row, no run tracking, no
    // hotkey, no startup refresh — and Mod Options shows nothing but this
    // toggle. Built by hand in ModMenu, which needs the Change handler to hide
    // the rest of the section
    [SettingIgnore]
    public bool Enabled { get; set; } = true;

    // the imported tabs of the practice sheet: "A Sides Standards" (all the
    // A-side checkpoints) and "B Sides Standards" (the any% route's 5B/6B
    // checkpoints), plus "Farewell Standards"
    public const string DefaultASidesUrl =
        SheetUrls.EditUrlPrefix + "1796170425";

    public const string DefaultBSidesUrl =
        SheetUrls.EditUrlPrefix + "1885706573";

    public const string DefaultFarewellUrl =
        SheetUrls.EditUrlPrefix + "1826331297";

    // full Google Sheets edit URLs (spreadsheet id + gid are extracted from
    // them); not editable in-game — change them in the settings file to read
    // another workbook. These are stored values: a player who has saved
    // settings keeps theirs, which is why SrsModule migrates the id of the
    // workbook srs read before 2026-08-28 (SheetUrls)
    [SettingIgnore]
    public string ASidesUrl { get; set; } = DefaultASidesUrl;

    [SettingIgnore]
    public string BSidesUrl { get; set; } = DefaultBSidesUrl;

    [SettingIgnore]
    public string FarewellUrl { get; set; } = DefaultFarewellUrl;

    // tier row drawn under the room timer once it completes; menu toggle +
    // rebindable hotkey, both handled in TierComparison
    [SettingIgnore]
    public bool ShowTier { get; set; } = true;

    [SettingIgnore]
    public ButtonBinding ToggleShowTier { get; set; } = new();

    // local date the URL was last set, "yyyy-MM-dd". Display only: it lets the
    // status line say "set <date>" without showing the URL
    [SettingIgnore]
    public string ExportUrlSetOn { get; set; } = "";

    // opens the export review screen; unbound by default. [SettingIgnore] and
    // read as a combo, like ToggleShowTier
    [SettingIgnore]
    public ButtonBinding OpenExportMenu { get; set; } = new();

}
