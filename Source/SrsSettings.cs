using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.SpeedrunSheet;

public class SrsSettings : EverestModuleSettings {
    // the two imported tabs of the practice sheet (v2.0.0): "A Sides
    // Standards" (all the A-side checkpoints) and "B Sides Standards" (the
    // any% route's 5B/6B checkpoints)
    public const string DefaultASidesUrl =
        "https://docs.google.com/spreadsheets/d/18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y/edit?gid=1796170425";

    public const string DefaultBSidesUrl =
        "https://docs.google.com/spreadsheets/d/18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y/edit?gid=1885706573";

    // full Google Sheets edit URLs (spreadsheet id + gid are extracted from
    // them); not editable in-game — change them in the settings file if the
    // sheet moves. The pre-2.0.0 single SheetUrl (old prototype sheet) is
    // simply dropped by the settings deserializer
    [SettingIgnore]
    public string ASidesUrl { get; set; } = DefaultASidesUrl;

    [SettingIgnore]
    public string BSidesUrl { get; set; } = DefaultBSidesUrl;

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

    [SettingIgnore]
    public string SelectedCheckpoint { get; set; } = "";

    // tier row drawn under the room timer once it completes; auto-generated
    // menu toggle + rebindable hotkey (handled in TierComparison)
    public bool ShowTier { get; set; } = true;

    [DefaultButtonBinding(0, Keys.None)]
    public ButtonBinding ToggleShowTier { get; set; }

    // the played checkpoint drives the selection (SegmentAutoDetect); the two
    // sliders become a manual override when turned off. Not auto-generated:
    // SegmentSelector builds the toggle itself so it can grey out the sliders
    [SettingIgnore]
    public bool AutoDetect { get; set; } = true;
}
