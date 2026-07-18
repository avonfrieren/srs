namespace Celeste.Mod.SpeedrunSheet;

public class SrsSettings : EverestModuleSettings {
    // full Google Sheets edit URL (spreadsheet id + gid are extracted from it);
    // not editable in-game — change it in Saves/modsettings-srs.celeste if the sheet moves
    [SettingIgnore]
    public string SheetUrl { get; set; } =
        "https://docs.google.com/spreadsheets/d/1WwqnaloO6I4gHcyzF3odz6n5LnSDTveS34BY-ljORuI/edit?gid=639470957";
}
