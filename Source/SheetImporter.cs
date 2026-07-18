using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Celeste.Mod.SpeedrunSheet;

// downloads the practice sheet as CSV (public "anyone with the link" sheet, no
// account/credentials involved) and keeps a local cache so the mod works offline
public static class SheetImporter {
    private const string LogTag = "srs";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static SheetData Data { get; private set; }
    public static DateTime? CacheTime { get; private set; }

    private static volatile bool updating;

    // the cache doubles as the manual-import fallback: dropping a hand-exported
    // sheet.csv at this path is equivalent to pressing the update button once
    public static string CachePath => Path.Combine(Everest.PathSettings, "srs", "sheet.csv");

    public static void Load() {
        try {
            if (File.Exists(CachePath)) {
                SheetData data = SheetData.Parse(File.ReadAllText(CachePath));
                if (data.SegmentCount > 0) {
                    Data = data;
                    CacheTime = File.GetLastWriteTime(CachePath);
                    Logger.Log(LogLevel.Info, LogTag, $"Loaded {data.SegmentCount} segments from cache ({CachePath})");
                } else {
                    Logger.Log(LogLevel.Warn, LogTag, $"Cache file has no usable segments ({CachePath})");
                }
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
            string url = ExportUrl(SrsModule.Settings.SheetUrl);
            if (url == null) {
                Logger.Log(LogLevel.Warn, LogTag, $"Could not extract a spreadsheet id from SheetUrl: {SrsModule.Settings.SheetUrl}");
                return false;
            }

            Logger.Log(LogLevel.Info, LogTag, $"Downloading sheet: {url}");
            string csv = await Http.GetStringAsync(url);

            // a private sheet answers 200 with a Google sign-in page instead of CSV
            if (csv.TrimStart().StartsWith("<", StringComparison.Ordinal)) {
                Logger.Log(LogLevel.Warn, LogTag, "Got HTML instead of CSV — is the sheet shared publicly (anyone with the link)?");
                return false;
            }

            SheetData data = SheetData.Parse(csv);
            if (data.SegmentCount == 0) {
                Logger.Log(LogLevel.Warn, LogTag, "Downloaded CSV contains no recognizable segments");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
            string tmp = CachePath + ".tmp";
            await File.WriteAllTextAsync(tmp, csv);
            File.Move(tmp, CachePath, overwrite: true);

            Data = data;
            CacheTime = DateTime.Now;
            Logger.Log(LogLevel.Info, LogTag, $"Sheet updated: {data.SegmentCount} segments in {data.Blocks.Count} blocks");
            return true;
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, LogTag, $"Sheet update failed: {e}");
            return false;
        }
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
