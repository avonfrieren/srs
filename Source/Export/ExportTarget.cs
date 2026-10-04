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

    /// False when the file could not be written, and the URL held is then
    /// unchanged. Called from a menu press, on the game thread.
    public static bool Set(string value) {
        if (!AtomicFile.TryWrite(FilePath, value, out Exception error, out Exception leftover)) {
            Logger.Log(LogLevel.Warn, LogTag, $"could not write the export URL file: {error.GetType().Name}");
            if (leftover != null) {
                // it holds the URL: say that it is still there, never what it holds
                Logger.Log(LogLevel.Warn, LogTag,
                    $"could not delete the temporary export URL file: {leftover.GetType().Name}");
            }

            return false;
        }

        url = value;
        return true;
    }

    /// False when the file could not be deleted, and the URL held then stays:
    /// the next launch would read it back.
    public static bool Forget() {
        try {
            if (File.Exists(FilePath)) {
                File.Delete(FilePath);
            }
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"could not delete the export URL file: {e.GetType().Name}");
            return false;
        }

        url = "";
        return true;
    }
}
