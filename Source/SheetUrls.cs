using System.Text.RegularExpressions;

namespace Celeste.Mod.SpeedrunSheet;

// Where the reference workbook lives, and how to move a player who is still
// pointed at the previous one. Game-free so the migration is testable.
//
// The three tab URLs are ordinary settings: [SettingIgnore] hides them from the
// menu, it does not stop them being serialized, and a stored value always beats
// a new default. Repointing the constants alone would move nobody who has ever
// saved settings — which is everybody.
public static class SheetUrls {
    // the reference workbook srs reads
    public const string ReferenceId = "1Gjr0t5Ncl30SnD34HYvdihVZToMMau3L2mw-b6XWSDY";

    // its predecessor, frozen 2026-08-28. It still answers, which is exactly
    // what makes it worth recognising: a player left on it keeps working and
    // stops seeing every later retiming. A value to migrate away from, never a
    // default
    private const string FrozenId = "18iSckSLnGQw13Ql_mpMLSVRbJKllp0lWZI6U0gP8x0Y";

    // the defaults in SrsSettings are built from this, so a future move cannot
    // repoint the constants and forget the migration
    public const string EditUrlPrefix =
        "https://docs.google.com/spreadsheets/d/" + ReferenceId + "/edit?gid=";

    /// Substitutes the frozen spreadsheet id, and nothing else, in a stored URL.
    /// Returns null when there is nothing to do, so the caller knows whether to
    /// save. The reference is a Drive copy of the frozen workbook and a Drive
    /// copy preserves sheetIds: every gid still names the same tab, so replacing
    /// the id alone keeps a gid a player changed on purpose. A URL naming any
    /// other workbook is left untouched.
    public static string Migrate(string url) {
        if (string.IsNullOrEmpty(url)) {
            return null;
        }

        // anchored on the /d/ segment and stopped before any further id
        // character, so this matches the id as a whole token and not as a prefix
        string migrated = Regex.Replace(url, "(?<=/d/)" + FrozenId + "(?![-\\w])", ReferenceId);
        return migrated == url ? null : migrated;
    }
}
