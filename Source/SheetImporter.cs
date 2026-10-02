using System;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Celeste.Mod.SpeedrunSheet;

// downloads the three practice sheet tabs as CSV (public "anyone with the
// link" sheet, no account/credentials involved) and keeps local caches so the
// mod works offline
public static class SheetImporter {
    private const string LogTag = "srs";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static SheetData Data { get; private set; }
    public static DateTime? CacheTime { get; private set; }

    // the download in flight, shared by the startup refresh and the menu
    // button: pressing the button during the startup one joins it instead of
    // downloading the same three tabs twice
    private static Task<bool> running;
    private static readonly object RunningGate = new();

    // the caches double as the manual-import fallback: dropping hand-exported
    // CSVs of the tabs at these paths is equivalent to pressing the update
    // button once. A cache missing farewell.csv still loads everything else,
    // and Farewell appears on the next update
    public static string ACachePath => Path.Combine(Everest.PathSettings, "srs", "asides.csv");
    public static string BCachePath => Path.Combine(Everest.PathSettings, "srs", "bsides.csv");
    public static string FarewellCachePath => Path.Combine(Everest.PathSettings, "srs", "farewell.csv");

    public static void Load() {
        try {
            string aSides = File.Exists(ACachePath) ? File.ReadAllText(ACachePath) : null;
            string bSides = File.Exists(BCachePath) ? File.ReadAllText(BCachePath) : null;
            string farewell = File.Exists(FarewellCachePath) ? File.ReadAllText(FarewellCachePath) : null;
            if (aSides == null && bSides == null && farewell == null) {
                return;
            }

            SheetData data = SheetData.Parse(aSides, bSides, farewell);
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
            // the sheet is retimed and extended regularly, so every launch
            // refreshes it in the background. The cache above is
            // already serving by then, and a failed download leaves it
            // untouched — offline play is unaffected, and a first launch with
            // no cache at all still ends up with data. Skipped while the mod is
            // switched off — nothing reads the result, and an off mod has no
            // business on the network; the master switch runs it on the way back on
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

        string date = CacheTime?.ToString("yyyy-MM-dd HH:mm") ?? "?";
        return $"{Dialog.Clean("SRS_STATUS_LOADED")}: {Data.SegmentCount} ({date})";
    }

    private static async Task<bool> UpdateFromSheet() {
        try {
            // the three at once: each is a round trip to Google, and none
            // depends on another
            Task<string> aTask = DownloadTab(SrsModule.Settings.ASidesUrl, "A Sides");
            Task<string> bTask = DownloadTab(SrsModule.Settings.BSidesUrl, "B Sides");
            Task<string> farewellTask = DownloadTab(SrsModule.Settings.FarewellUrl, "Farewell");
            string[] tabs = await Task.WhenAll(aTask, bTask, farewellTask);
            string aSides = tabs[0], bSides = tabs[1], farewell = tabs[2];
            // all or nothing: a half-updated cache would silently drop whole
            // chapters from the sliders
            if (aSides == null || bSides == null || farewell == null) {
                return false;
            }

            SheetData data = SheetData.Parse(aSides, bSides, farewell);
            if (data.SegmentCount == 0) {
                Logger.Log(LogLevel.Warn, LogTag, "Downloaded CSVs contain no recognizable segments");
                return false;
            }

            WriteCache(ACachePath, aSides);
            WriteCache(BCachePath, bSides);
            WriteCache(FarewellCachePath, farewell);

            Data = data;
            CacheTime = DateTime.Now;
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
                + string.Join(", ", data.MissingRows.Select(row => $"{row.Chapter} / {row.Name}")));
        }
    }

    private static async Task<string> DownloadTab(string sheetUrl, string label) {
        string url = ExportUrl(sheetUrl);
        if (url == null) {
            Logger.Log(LogLevel.Warn, LogTag, $"Could not extract a spreadsheet id from the {label} url: {sheetUrl}");
            return null;
        }

        Logger.Log(LogLevel.Info, LogTag, $"Downloading {label} tab: {url}");
        string csv = await Http.GetStringAsync(url);

        // a private sheet answers 200 with a Google sign-in page instead of CSV
        if (csv.TrimStart().StartsWith("<", StringComparison.Ordinal)) {
            Logger.Log(LogLevel.Warn, LogTag, $"Got HTML instead of CSV for the {label} tab — is the sheet shared publicly (anyone with the link)?");
            return null;
        }

        return csv;
    }

    private static void WriteCache(string path, string csv) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, csv);
        File.Move(tmp, path, overwrite: true);
    }

    private static string CachePaths => string.Join(", ", CacheFiles);

    private static string[] CacheFiles => [ACachePath, BCachePath, FarewellCachePath];

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

    // accepts a full edit URL (or just an id) and builds the no-auth CSV export URL
    public static string ExportUrl(string sheetUrl) {
        if (string.IsNullOrWhiteSpace(sheetUrl)) {
            return null;
        }

        Match id = Regex.Match(sheetUrl, @"/d/([\w-]+)");
        if (!id.Success) {
            return null;
        }

        Match gid = Regex.Match(sheetUrl, @"[?#&]gid=(\d+)");
        return $"https://docs.google.com/spreadsheets/d/{id.Groups[1].Value}/export?format=csv&gid={(gid.Success ? gid.Groups[1].Value : "0")}";
    }
}
