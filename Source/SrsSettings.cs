namespace Celeste.Mod.SpeedrunSheet;

public class SrsSettings : EverestModuleSettings {
    // master switch: off, the mod is inert and Mod Options shows only this
    // toggle. Built by hand in ModMenu, whose Change handler hides the rest
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

    // full edit URLs, editable only in the settings file. Stored values: see
    // SheetUrls for why SrsModule migrates them
    [SettingIgnore]
    public string ASidesUrl { get; set; } = DefaultASidesUrl;

    [SettingIgnore]
    public string BSidesUrl { get; set; } = DefaultBSidesUrl;

    [SettingIgnore]
    public string FarewellUrl { get; set; } = DefaultFarewellUrl;

    // tier row drawn under the room timer for the latest record; menu toggle +
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
