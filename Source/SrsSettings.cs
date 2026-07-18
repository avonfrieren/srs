namespace Celeste.Mod.SpeedrunSheet;

public class SrsSettings : EverestModuleSettings {
    // the "Celeste Any% Standards CP's" tab (gid 276158492): every checkpoint
    // of the any% route, grouped by chapter
    public const string DefaultSheetUrl =
        "https://docs.google.com/spreadsheets/d/1WwqnaloO6I4gHcyzF3odz6n5LnSDTveS34BY-ljORuI/edit?gid=276158492";

    // pre-v0.2.0 default: the "Standards IL's" tab (whole chapters only);
    // settings files still carrying it are migrated in SrsModule.LoadSettings
    public const string LegacySheetUrl =
        "https://docs.google.com/spreadsheets/d/1WwqnaloO6I4gHcyzF3odz6n5LnSDTveS34BY-ljORuI/edit?gid=639470957";

    // full Google Sheets edit URL (spreadsheet id + gid are extracted from it);
    // not editable in-game — change it in Saves/modsettings-srs.celeste if the sheet moves
    [SettingIgnore]
    public string SheetUrl { get; set; } = DefaultSheetUrl;

    // checkpoint selected in Mod Options, addressed by name so the selection
    // survives sheet re-imports; empty until first picked
    [SettingIgnore]
    public string SelectedChapter { get; set; } = "";

    [SettingIgnore]
    public string SelectedCheckpoint { get; set; } = "";
}
