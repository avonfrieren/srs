namespace Celeste.Mod.SpeedrunSheet;

/// A writable row of the personal practice sheet, identified by its tab and its
/// two label columns. Chapter is empty on the Farewell tab, which has none.
public readonly record struct SheetRowRef(string Tab, string Chapter, string Cp);

/// Translation from srs's own (chapter, checkpoint) naming to the row it is
/// written to in the player's sheet. It lives in SheetRows, and a row absent
/// from there is never exported.
public static class SheetLabels {
    public const string TabASides = "A Sides";
    public const string TabBCSides = "B+C Sides";
    public const string TabFarewell = "Farewell";

    public static bool TryMap(string srsChapter, string srsName, out SheetRowRef row) {
        if (SheetRows.TryFind(srsChapter, srsName, out SheetRow sheetRow)) {
            row = SheetRows.TargetOf(sheetRow);
            return true;
        }

        row = default;
        return false;
    }
}
