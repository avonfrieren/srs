using System.Collections.Generic;
using Xunit;

namespace Celeste.Mod.SpeedrunSheet.Tests;

public class ExportProtocolTests {
    [Fact]
    public void SerializesARequestWithLowerCamelCaseFields() {
        var request = new ExportRequest {
            Updates = { new ExportUpdate { Chapter = "6b", Cp = "Falling", Time = "1:07.915" } },
        };

        string json = ExportProtocol.SerializeRequest(request);

        Assert.Contains("\"chapter\":\"6b\"", json);
        Assert.Contains("\"time\":\"1:07.915\"", json);
    }

    [Fact]
    public void SerializesEmojiWithoutEscapingThemIntoSurrogatePairs() {
        var request = new ExportRequest {
            Updates = { new ExportUpdate { Chapter = "6a", Cp = "Hollows \U0001F4FC", Time = "8.704" } },
        };

        string json = ExportProtocol.SerializeRequest(request);

        Assert.Contains("\U0001F4FC", json);
    }

    // the payloads below are what the deployed script actually answered on
    // 2026-08-28, copied verbatim. They used to be invented, and they invented
    // an "ok" and a "row" the script has never sent
    [Fact]
    public void ParsesAResponse() {
        const string json = """
            {"results":[{"tab":"A Sides","chapter":"7a","cp":"0m \uD83D\uDC8E","status":"written","reason":""}]}
            """;

        Assert.True(ExportProtocol.TryParseResponse(json, out var response, out string error));
        Assert.Null(error);
        Assert.Single(response.Results);
        Assert.Equal("written", response.Results[0].Status);
        Assert.Equal("0m \U0001F48E", response.Results[0].Cp);
    }

    [Fact]
    public void ParsesARefusalWithItsReason() {
        const string json = """
            {"results":[{"tab":"A Sides","chapter":"1a","cp":"No Such Row","status":"notFound","reason":"no row matching 1a / No Such Row in tab \"A Sides\""}]}
            """;

        Assert.True(ExportProtocol.TryParseResponse(json, out var response, out _));
        Assert.Equal("notFound", response.Results[0].Status);
        Assert.StartsWith("no row matching", response.Results[0].Reason);
    }

    // the write path answers with results or with error, never both, and both
    // parse entry points treat error the same way: a failed parse carrying the
    // script's message, not a response the caller has to inspect for one
    [Fact]
    public void AResponseErrorSurfacesTheScriptsDiagnosticMessage() {
        const string json = """
            {"error":"TypeError: Cannot read properties of null"}
            """;

        Assert.False(ExportProtocol.TryParseResponse(json, out var response, out string error));
        Assert.Null(response);
        Assert.Equal("TypeError: Cannot read properties of null", error);
    }

    [Fact]
    public void RejectsAnHtmlBodyWithAnExplicitMessage() {
        Assert.False(ExportProtocol.TryParseResponse("<!DOCTYPE html><html>", out _, out string error));
        // unlocalised in the test project: ExportProtocol.Localize is left as identity
        Assert.Equal("SRS_EXPORT_ERR_LOGIN_PAGE", error);
    }

    [Fact]
    public void RejectsMalformedJsonWithoutThrowing() {
        Assert.False(ExportProtocol.TryParseResponse("{not json", out _, out string error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ParsesRemoteRows() {
        const string json = """
            {"rows":[{"tab":"A Sides","chapter":"1a","cp":"Crossing","time":"21.947","standard":"Pink"}]}
            """;

        Assert.True(ExportProtocol.TryParseRows(json, out List<RemoteRow> rows, out _));
        Assert.Single(rows);
        Assert.Equal("A Sides", rows[0].Tab);
        Assert.Equal("Crossing", rows[0].Cp);
        Assert.Equal("Pink", rows[0].Standard);
    }

    [Fact]
    public void RemoteRowsErrorSurfacesTheScriptsDiagnosticMessage() {
        const string json = """
            {"error":"Error: Tab \"Any%\" not found"}
            """;

        Assert.False(ExportProtocol.TryParseRows(json, out _, out string error));
        Assert.Equal("Error: Tab \"Any%\" not found", error);
    }
}
