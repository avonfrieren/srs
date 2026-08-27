using Microsoft.Xna.Framework.Input;

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
        "https://docs.google.com/spreadsheets/d/18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y/edit?gid=1796170425";

    public const string DefaultBSidesUrl =
        "https://docs.google.com/spreadsheets/d/18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y/edit?gid=1885706573";

    public const string DefaultFarewellUrl =
        "https://docs.google.com/spreadsheets/d/18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y/edit?gid=1826331297";

    // full Google Sheets edit URLs (spreadsheet id + gid are extracted from
    // them); not editable in-game — change them in the settings file if the
    // sheet moves. The pre-2.0.0 single SheetUrl (old prototype sheet) is
    // simply dropped by the settings deserializer
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
    // gesture (any% run, then the cassette variant of the same checkpoint)
    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding CycleCategory { get; set; }

    [SettingIgnore]
    public string SelectedCheckpoint { get; set; } = "";

    // tier row drawn under the room timer once it completes; menu toggle +
    // rebindable hotkey, both handled in TierComparison
    [SettingIgnore]
    public bool ShowTier { get; set; } = true;

    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ToggleShowTier { get; set; }

    // discreet "category - checkpoint" row under the tier row (v3.3.0): what
    // the next run will be compared against, readable at a glance without
    // opening Mod Options. Same shape as ShowTier — menu toggle + rebindable
    // hotkey, both handled in TierComparison
    [SettingIgnore]
    public bool ShowSelection { get; set; } = true;

    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ToggleShowSelection { get; set; }

    // the played checkpoint drives the selection (SegmentAutoDetect); the two
    // sliders become a manual override when turned off. Not auto-generated:
    // SegmentSelector builds the toggle itself so it can grey out the sliders
    [SettingIgnore]
    public bool AutoDetect { get; set; } = true;
}
