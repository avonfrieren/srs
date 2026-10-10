using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Celeste.Mod.SpeedrunSheet;

// downloads the practice sheet's Standards tabs as CSV (public "anyone with the
// link" sheet, no account/credentials involved) and keeps local caches so the
// mod works offline
public static class SheetImporter {
    private const string LogTag = "srs";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static SheetData Data { get; private set; }
    public static DateTime? CacheTime { get; private set; }

    // the download in flight, shared by the startup refresh and the menu
    // button: pressing the button during the startup one joins it instead of
    // downloading the same tabs twice
    private static Task<bool> running;
    private static readonly object RunningGate = new();

    // the caches double as the manual import: hand-exported CSVs of the tabs
    // dropped at these paths load like downloaded ones. A cache missing some
    // files still loads the others
    private static string CachePathOf(StandardsTabInfo tab) =>
        Path.Combine(Everest.PathSettings, "srs", tab.CacheFile);

    public static void Load() {
        try {
            Dictionary<StandardsTab, string> cached = [];
            foreach (StandardsTabInfo tab in StandardsTabs.All) {
                string path = CachePathOf(tab);
                if (File.Exists(path)) {
                    cached[tab.Tab] = File.ReadAllText(path);
                }
            }

            if (cached.Count == 0) {
                return;
            }

            SheetData data = SheetData.Parse(cached);
            if (data.SegmentCount > 0) {
                Data = data;
                CacheTime = LatestCacheTime();
                Logger.Log(LogLevel.Info, LogTag, $"Loaded {data.SegmentCount} segments from cache ({CachePaths})");
                // here too, not only after a download: offline, the cache is
                // all a player ever runs on
                LogMissingRows(data, "the cached sheet");
            } else {
                Logger.Log(LogLevel.Warn, LogTag, $"Cache files have no usable segments ({CachePaths})");
            }
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"Failed to load sheet cache: {e}");
        } finally {
            // every launch refreshes the sheet in the background, behind the
            // cache already serving: a failed download leaves it untouched. Not
            // while the mod is off: the master switch runs it on the way back on
            if (SrsModule.Settings.Enabled) {
                BeginUpdate(null);
            }
        }
    }

    public static void Unload() {
        Data = null;
        CacheTime = null;
    }

    /// Returns what pressing Update Standards does, for the master switch to
    /// run when the mod comes back on: the same refresh, shown the same way.
    public static Action CreateMenuEntries(TextMenu menu) {
        TextMenu.SubHeader status = new(StatusText(), topPadding: false);
        TextMenu.Button update = new(Dialog.Clean("SRS_UPDATE_STANDARDS"));
        void Refresh() {
            update.Label = Dialog.Clean("SRS_UPDATING");
            // menu items just read these strings each frame, so mutating them
            // from the worker thread is safe
            BeginUpdate(ok => {
                update.Label = Dialog.Clean(ok ? "SRS_UPDATE_OK" : "SRS_UPDATE_FAIL");
                status.Title = StatusText();
            });
        }

        update.Pressed(Refresh);
        menu.Add(update);
        menu.Add(status);
        return Refresh;
    }

    // starts a refresh unless one is already running, and reports its outcome
    // to onDone either way
    internal static void BeginUpdate(Action<bool> onDone) {
        Task<bool> task;
        lock (RunningGate) {
            if (running == null || running.IsCompleted) {
                running = Task.Run(UpdateFromSheet);
            }

            task = running;
        }

        if (onDone != null) {
            task.ContinueWith(finished =>
                onDone(finished.Status == TaskStatus.RanToCompletion && finished.Result));
        }
    }

    private static string StatusText() {
        if (Data == null) {
            return Dialog.Clean("SRS_STATUS_NONE");
        }

        string date = CacheTime?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "?";
        return $"{Dialog.Clean("SRS_STATUS_LOADED")}: {Data.SegmentCount} ({date})";
    }

    private static async Task<bool> UpdateFromSheet() {
        try {
            // all at once: each is a round trip to Google, and none depends on
            // another
            StandardsTabInfo[] tabs = StandardsTabs.All;
            string[] csvs = await Task.WhenAll(
                tabs.Select(tab => DownloadTab(SrsModule.Settings.UrlOf(tab.Tab), tab.LogName)));
            // all or nothing: a half-updated cache would silently drop whole
            // rows from the tracked set
            if (csvs.Contains(null)) {
                return false;
            }

            Dictionary<StandardsTab, string> downloaded = [];
            for (int i = 0; i < tabs.Length; i++) {
                downloaded[tabs[i].Tab] = csvs[i];
            }

            SheetData data = SheetData.Parse(downloaded);
            if (data.SegmentCount == 0) {
                Logger.Log(LogLevel.Warn, LogTag, "Downloaded CSVs contain no recognizable segments");
                return false;
            }

            // before the caches: a file that cannot be written must not cost
            // the session what was just downloaded
            Data = data;
            CacheTime = DateTime.Now;
            WriteCaches(tabs.Select(CachePathOf).ToArray(), csvs);
            Logger.Log(LogLevel.Info, LogTag, $"Sheet updated: {data.SegmentCount} segments");
            LogMissingRows(data, "the sheet");
            return true;
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"Sheet update failed: {e}");
            return false;
        }
    }

    // the log only, never the menu: a row the sheet renamed is the
    // maintainer's to fix, and the player can do nothing about it
    private static void LogMissingRows(SheetData data, string where) {
        if (data.MissingRows.Count > 0) {
            Logger.Log(LogLevel.Warn, LogTag,
                $"{data.MissingRows.Count} imported row(s) not in {where}: "
                + string.Join(", ", data.MissingRows.Select(row => $"{row.SheetChapter} / {row.Label}")));
        }
    }

    private static async Task<string> DownloadTab(string sheetUrl, string label) {
        string url = SheetUrls.CsvUrlOf(sheetUrl);
        if (url == null) {
            Logger.Log(LogLevel.Warn, LogTag, $"Could not extract a spreadsheet id from the {label} url: {sheetUrl}");
            return null;
        }

        Logger.Log(LogLevel.Info, LogTag, $"Downloading {label} tab: {url}");
        string csv;
        try {
            csv = await Http.GetStringAsync(url);
        } catch (Exception e) {
            // here and not in UpdateFromSheet's catch, to name the tab
            Logger.Log(LogLevel.Warn, LogTag, $"Download of the {label} tab failed: {e.GetType().Name}: {e.Message}");
            return null;
        }

        // a private sheet answers 200 with a Google sign-in page instead of CSV
        if (csv.TrimStart().StartsWith("<", StringComparison.Ordinal)) {
            Logger.Log(LogLevel.Warn, LogTag, $"Got HTML instead of CSV for the {label} tab: is the sheet shared publicly (anyone with the link)?");
            return null;
        }

        return csv;
    }

    // every file is written beside its cache before any is moved over it, so
    // a write that fails leaves the caches of one download
    private static void WriteCaches(string[] paths, string[] csvs) {
        try {
            for (int i = 0; i < paths.Length; i++) {
                Directory.CreateDirectory(Path.GetDirectoryName(paths[i]));
                File.WriteAllText(paths[i] + ".tmp", csvs[i]);
            }

            foreach (string path in paths) {
                File.Move(path + ".tmp", path, overwrite: true);
            }
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"Could not cache the sheet: {e.GetType().Name}: {e.Message}");
        }
    }

    private static string CachePaths => string.Join(", ", CacheFiles);

    private static string[] CacheFiles => [.. StandardsTabs.All.Select(CachePathOf)];

    private static DateTime? LatestCacheTime() {
        DateTime? latest = null;
        foreach (string path in CacheFiles) {
            if (File.Exists(path)) {
                DateTime time = File.GetLastWriteTime(path);
                if (latest == null || time > latest) {
                    latest = time;
                }
            }
        }

        return latest;
    }
}
