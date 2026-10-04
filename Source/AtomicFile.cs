using System;
using System.IO;

namespace Celeste.Mod.SpeedrunSheet;

/// A file replaced whole or not at all, for writes made on the game thread.
internal static class AtomicFile {
    /// Writes beside the file, then moves over it. Never throws: a failure comes
    /// back in error, and the temporary file is deleted, since it may hold the
    /// export URL. leftover is why that delete failed too, null when it did not.
    public static bool TryWrite(string path, string text, out Exception error, out Exception leftover) {
        string tmp = path + ".tmp";
        leftover = null;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, overwrite: true);
            error = null;
            return true;
        } catch (Exception e) {
            error = e;
            try {
                File.Delete(tmp);
            } catch (Exception cleanup) {
                leftover = cleanup;
            }

            return false;
        }
    }
}
