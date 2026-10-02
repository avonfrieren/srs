using System;
using System.IO;

namespace Celeste.Mod.SpeedrunSheet;

/// The player's Apps Script Web App URL. It *is* the credential, the Web App
/// has no auth of its own, so it is never logged or displayed.
///
/// ⚠️ Kept out of the settings file on purpose: players share
/// modsettings-*.celeste when asking for help, and a URL in there would be
/// handed over with it. It lives in its own file beside the standards cache.
internal static class ExportTarget {
    private const string LogTag = "srs";

    private static string FilePath => Path.Combine(Everest.PathSettings, "srs", "export-url.txt");

    // read from the workers resolving a fetch, which compare it against the
    // URL they asked
    private static volatile string url = "";

    public static string Url => url;

    public static bool IsSet => !string.IsNullOrEmpty(url);

    /// Before anything reads Url: ExportMenu.Load asks the sheet at launch.
    public static void Load() {
        try {
            url = File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
        } catch (Exception e) {
            // never the path's content: an unreadable file says nothing about the URL
            Logger.Log(LogLevel.Warn, LogTag, $"could not read the export URL file: {e.GetType().Name}");
            url = "";
        }
    }

    public static void Set(string value) {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, value);
        File.Move(tmp, FilePath, overwrite: true);
        url = value;
    }

    public static void Forget() {
        url = "";
        try {
            File.Delete(FilePath);
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"could not delete the export URL file: {e.GetType().Name}");
        }
    }
}
