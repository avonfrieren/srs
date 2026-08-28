using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

/// Talks to the player's Apps Script Web App. Never runs on the game thread and
/// never throws: failures come back as a message in the second tuple slot.
internal static class ExportClient {
    private const string LogTag = "srs";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static Task<(string body, string error)> FetchAsync(string url) =>
        SendAsync(url, null);

    public static Task<(string body, string error)> PostAsync(string url, string json) =>
        SendAsync(url, json);

    private static Task<(string body, string error)> SendAsync(string url, string json) =>
        Task.Run(async () => {
            if (string.IsNullOrWhiteSpace(url)) {
                return (null, Dialog.Clean("SRS_EXPORT_ERR_NO_URL"));
            }
            try {
                HttpResponseMessage response = json == null
                    ? await Http.GetAsync(url)
                    : await Http.PostAsync(url,
                        new StringContent(json, Encoding.UTF8, "application/json"));

                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) {
                    // never log the URL: it is a secret
                    Logger.Log(LogLevel.Warn, LogTag, $"export request failed: {(int) response.StatusCode}");
                    return (null, $"{Dialog.Clean("SRS_EXPORT_ERR_STATUS")} {(int) response.StatusCode}.");
                }
                return (body, (string) null);
            } catch (TaskCanceledException) {
                Logger.Log(LogLevel.Warn, LogTag, "export request timed out after 60s");
                // the script may have run to completion server-side: a timeout
                // says nothing about whether the sheet was written
                return (null, Dialog.Clean("SRS_EXPORT_ERR_TIMEOUT"));
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, LogTag, "export request failed: " + e.Message);
                return (null, Dialog.Clean("SRS_EXPORT_ERR_UNREACHABLE") + " " + e.Message);
            }
        });
}
