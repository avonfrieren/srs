using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Celeste.Mod.SpeedrunSheet;

public sealed class ExportUpdate {
    [JsonPropertyName("tab")] public string Tab { get; set; } = "";
    [JsonPropertyName("chapter")] public string Chapter { get; set; } = "";
    [JsonPropertyName("cp")] public string Cp { get; set; } = "";
    [JsonPropertyName("time")] public string Time { get; set; } = "";
    /// what srs believed the cell held, as the sheet displayed it. The script
    /// refuses the row rather than writing when it no longer matches.
    [JsonPropertyName("expect")] public string Expect { get; set; } = "";
}

public sealed class ExportRequest {
    [JsonPropertyName("updates")] public List<ExportUpdate> Updates { get; set; } = [];
}

public sealed class ExportResult {
    [JsonPropertyName("tab")] public string Tab { get; set; } = "";
    [JsonPropertyName("chapter")] public string Chapter { get; set; } = "";
    [JsonPropertyName("cp")] public string Cp { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
}

/// The script answers with results, or with error and nothing else. There is no
/// "ok" on the wire: a run against the deployed script on 2026-08-28 returned
/// exactly {results:[...]} and {error:"..."}.
public sealed class ExportResponse {
    [JsonPropertyName("results")] public List<ExportResult> Results { get; set; } = [];
    [JsonPropertyName("error")] public string Error { get; set; }
}

public sealed class RemoteRow {
    [JsonPropertyName("tab")] public string Tab { get; set; } = "";
    [JsonPropertyName("chapter")] public string Chapter { get; set; } = "";
    [JsonPropertyName("cp")] public string Cp { get; set; } = "";
    [JsonPropertyName("time")] public string Time { get; set; } = "";
    // sent by the script and not read yet, unlike the fields removed around it:
    // this one is on the wire, it is the tier the sheet itself assigns
    [JsonPropertyName("standard")] public string Standard { get; set; } = "";
}

internal sealed class RowsResponse {
    [JsonPropertyName("rows")] public List<RemoteRow> Rows { get; set; }
    // how long the script itself took, so the wait can be split between its
    // work and Google's dispatch. Absent from an older script
    [JsonPropertyName("ms")] public int? Ms { get; set; }
    [JsonPropertyName("cached")] public bool Cached { get; set; }
    [JsonPropertyName("error")] public string Error { get; set; }
}

/// Wire format between the mod and the player's Apps Script Web App.
/// Never throws: every failure path returns false with a human-readable message.
public static class ExportProtocol {
    /// Set to Dialog.Clean by ExportMenu.Load(). This file is compiled straight
    /// into the test project, which has no game reference, so the lookup has to
    /// come in from outside; unset, it hands back the key.
    public static Func<string, string> Localize = key => key;

    private static readonly JsonSerializerOptions Options = new() {
        // Keep non-ASCII (accents, etc.) as literal characters instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // System.Text.Encodings.Web has no way to leave supplementary-plane characters (emoji, U+10000+)
    // unescaped: JavaScriptEncoder always escapes surrogate pairs into 📼-style sequences,
    // even with UnsafeRelaxedJsonEscaping or an explicit AllowCodePoints() for that scalar value
    // (verified against .NET 8.0.24 — this is a known runtime limitation, not a config we're missing).
    // The escaped form is valid JSON either way, so unescape just the surrogate-pair case afterwards
    // to keep emoji literal on the wire, matching what the Apps Script side expects to log/compare.
    private static readonly Regex SurrogatePairEscape =
        new(@"\\u(d[89ab][0-9a-f]{2})\\u(d[c-f][0-9a-f]{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// A deployed Apps Script Web App endpoint. The /dev URL of the same
    /// script answers a signed-out client with a login page, so it is refused
    /// here rather than accepted and left to fail at export time.
    public static bool IsEndpointUrl(string url) {
        if (string.IsNullOrWhiteSpace(url)) {
            return false;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.AbsolutePath.EndsWith("/exec", StringComparison.Ordinal);
    }

    public static string SerializeRequest(ExportRequest request) {
        string json = JsonSerializer.Serialize(request, Options);
        return SurrogatePairEscape.Replace(json, m => {
            char high = (char) Convert.ToInt32(m.Groups[1].Value, 16);
            char low = (char) Convert.ToInt32(m.Groups[2].Value, 16);
            return new string(new[] { high, low });
        });
    }

    public static bool TryParseResponse(string body, out ExportResponse response, out string error) {
        response = null;
        if (!Guard(body, out error)) {
            return false;
        }
        try {
            response = JsonSerializer.Deserialize<ExportResponse>(body, Options);
        } catch (JsonException e) {
            error = Localize("SRS_EXPORT_ERR_UNREADABLE") + " " + e.Message;
            return false;
        }
        if (response == null) {
            error = Localize("SRS_EXPORT_ERR_EMPTY");
            return false;
        }
        // same shape as TryParseRows: the script answers with results or with
        // error, never both, so a body carrying error is a failed parse and not
        // a response the caller has to inspect for one
        if (!string.IsNullOrEmpty(response.Error)) {
            error = response.Error;
            response = null;
            return false;
        }
        error = null;
        return true;
    }

    public static bool TryParseRows(string body, out List<RemoteRow> rows, out string error) =>
        TryParseRows(body, out rows, out string _, out error);

    public static bool TryParseRows(string body, out List<RemoteRow> rows, out string scriptTiming,
        out string error) {
        scriptTiming = null;
        rows = null;
        if (!Guard(body, out error)) {
            return false;
        }
        RowsResponse wrapper;
        try {
            wrapper = JsonSerializer.Deserialize<RowsResponse>(body, Options);
        } catch (JsonException e) {
            error = Localize("SRS_EXPORT_ERR_UNREADABLE") + " " + e.Message;
            return false;
        }
        if (wrapper == null) {
            error = Localize("SRS_EXPORT_ERR_EMPTY");
            return false;
        }
        if (!string.IsNullOrEmpty(wrapper.Error)) {
            error = wrapper.Error;
            return false;
        }
        if (wrapper.Rows == null) {
            error = Localize("SRS_EXPORT_ERR_NO_ROWS");
            return false;
        }
        rows = wrapper.Rows;
        scriptTiming = wrapper.Ms is { } ms
            ? $"{ms} ms in the script{(wrapper.Cached ? ", cached" : "")}"
            : "script timing unknown";
        error = null;
        return true;
    }

    /// An HTML body means Google served a login page: the Web App was deployed with
    /// "Only myself" instead of "Anyone", so a plain HTTP request gets rejected.
    private static bool Guard(string body, out string error) {
        if (string.IsNullOrWhiteSpace(body)) {
            error = Localize("SRS_EXPORT_ERR_EMPTY");
            return false;
        }
        string head = body.TrimStart();
        if (head.StartsWith("<", StringComparison.Ordinal)) {
            error = Localize("SRS_EXPORT_ERR_LOGIN_PAGE");
            return false;
        }
        error = null;
        return true;
    }
}
