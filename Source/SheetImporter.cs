using System;
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

    private static volatile bool updating;

    // the caches double as the manual-import fallback: dropping hand-exported
    // CSVs of the tabs at these paths is equivalent to pressing the update
    // button once. A cache from before v3.4.0 simply has no farewell.csv —
    // everything else still loads, Farewell appears on the next update
    public static string ACachePath => Path.Combine(Everest.PathSettings, "srs", "asides.csv");
    public static string BCachePath => Path.Combine(Everest.PathSettings, "srs", "bsides.csv");
    public static string FarewellCachePath => Path.Combine(Everest.PathSettings, "srs", "farewell.csv");

    public static void Load() {
        try {
            // pre-2.0.0 single-tab cache: the old prototype sheet's CSV, which
            // the current parser has no rows for — clean it up
            File.Delete(Path.Combine(Everest.PathSettings, "srs", "sheet.csv"));
        } catch (Exception) {
            // fine, it just was not there (or is unreadable — harmless either way)
        }

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
            } else {
                Logger.Log(LogLevel.Warn, LogTag, $"Cache files have no usable segments ({CachePaths})");
            }
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"Failed to load sheet cache: {e}");
        }
    }

    public static void Unload() {
        Data = null;
        CacheTime = null;
    }

    public static void CreateMenuEntries(TextMenu menu) {
        TextMenu.SubHeader status = new(StatusText(), topPadding: false);
        TextMenu.Button update = new(Dialog.Clean("SRS_UPDATE_SHEET"));
        update.Pressed(() => {
            if (updating) {
                return;
            }

            updating = true;
            update.Label = Dialog.Clean("SRS_UPDATING");
            Task.Run(UpdateFromSheet).ContinueWith(task => {
                // menu items just read these strings each frame, so mutating them
                // from the worker thread is safe
                bool ok = task.Status == TaskStatus.RanToCompletion && task.Result;
                update.Label = Dialog.Clean(ok ? "SRS_UPDATE_OK" : "SRS_UPDATE_FAIL");
                status.Title = StatusText();
                updating = false;
            });
        });
        menu.Add(update);
        menu.Add(status);
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
            string aSides = await DownloadTab(SrsModule.Settings.ASidesUrl, "A Sides");
            string bSides = await DownloadTab(SrsModule.Settings.BSidesUrl, "B Sides");
            string farewell = await DownloadTab(SrsModule.Settings.FarewellUrl, "Farewell");
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
            return true;
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"Sheet update failed: {e}");
            return false;
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
