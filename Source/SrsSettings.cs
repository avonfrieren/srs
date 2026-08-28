namespace Celeste.Mod.SpeedrunSheet;

public class SrsSettings : EverestModuleSettings {
    // master switch. Off, the mod is inert — no HUD row, no auto-detection, no
    // hotkey, no startup refresh — and Mod Options shows nothing but this
    // toggle. Built by hand in ModMenu, which needs the Change handler to hide
    // the rest of the section
    [SettingIgnore]
    public bool Enabled { get; set; } = true;

    // the imported tabs of the practice sheet: "A Sides Standards" (all the
    // A-side checkpoints) and "B Sides Standards" (the any% route's 5B/6B
    // checkpoints) since v2.0.0, plus "Farewell Standards" since v3.4.0
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
    // workbook srs read before 2026-08-28 (SheetUrls). The pre-2.0.0 single
    // SheetUrl (old prototype sheet) is simply dropped by the deserializer
    [SettingIgnore]
    public string ASidesUrl { get; set; } = DefaultASidesUrl;

    [SettingIgnore]
    public string BSidesUrl { get; set; } = DefaultBSidesUrl;

    [SettingIgnore]
    public string FarewellUrl { get; set; } = DefaultFarewellUrl;

    // checkpoint selected in Mod Options, addressed by name so the selection
    // survives sheet re-imports; empty until first picked
    [SettingIgnore]
    public string SelectedChapter { get; set; } = "";

    // category being practiced: auto-detection resolves the checkpoints that
    // exist in several variants with it (Cassette: Hollows -> Hollows Tape).
    // Not auto-generated — SegmentSelector builds the slider itself, next to
    // the chapter/checkpoint ones
    [SettingIgnore]
    public SegmentCategory Category { get; set; } = SegmentCategory.AnyPercent;

    // rebindable hotkey cycling Category without opening Mod Options (v3.1.0),
    // handled in SegmentAutoDetect — switching category is a mid-practice
    // gesture (any% run, then the cassette variant of the same checkpoint).
    // [SettingIgnore] keeps it out of Everest's key config screen: the three
    // hotkeys are read as combos (Hotkeys/ComboHotkey), which that screen has
    // no way to express, and KeybindConfigUi binds them instead
    [SettingIgnore]
    public ButtonBinding CycleCategory { get; set; } = new();

    [SettingIgnore]
    public string SelectedCheckpoint { get; set; } = "";

    // tier row drawn under the room timer once it completes; menu toggle +
    // rebindable hotkey, both handled in TierComparison
    [SettingIgnore]
    public bool ShowTier { get; set; } = true;

    [SettingIgnore]
    public ButtonBinding ToggleShowTier { get; set; } = new();

    // discreet "category - checkpoint" row under the tier row (v3.3.0): what
    // the next run will be compared against, readable at a glance without
    // opening Mod Options. Same shape as ShowTier — menu toggle + rebindable
    // hotkey, both handled in TierComparison
    [SettingIgnore]
    public bool ShowSelection { get; set; } = true;

    [SettingIgnore]
    public ButtonBinding ToggleShowSelection { get; set; } = new();

    // the played checkpoint drives the selection (SegmentAutoDetect); the two
    // sliders become a manual override when turned off. Not auto-generated:
    // SegmentSelector builds the toggle itself so it can grey out the sliders
    [SettingIgnore]
    public bool AutoDetect { get; set; } = true;

    // the player's Apps Script Web App URL. The URL *is* the credential — the
    // Web App has no auth of its own — so it is never logged or displayed;
    // ExportUrlMenu sets it through an always-empty field
    [SettingIgnore]
    public string ExportUrl { get; set; } = "";

    // local date the URL was last set, "yyyy-MM-dd". Display only: it lets the
    // status line say "set <date>" without showing the URL
    [SettingIgnore]
    public string ExportUrlSetOn { get; set; } = "";

    // opens the export review screen; unbound by default. [SettingIgnore] and
    // read as a combo like the other three — see CycleCategory
    [SettingIgnore]
    public ButtonBinding OpenExportMenu { get; set; } = new();

}
