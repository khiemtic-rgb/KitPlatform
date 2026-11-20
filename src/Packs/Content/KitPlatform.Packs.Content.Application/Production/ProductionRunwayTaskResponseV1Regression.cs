namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Runway task JSON keeps the provider failure code apart from PROVIDER_UNAVAILABLE.
/// No network. Output URLs and secrets are not retained.
/// </summary>
public static class ProductionRunwayTaskResponseV1Regression
{
    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var code01 = RunwayTaskResponse.Read(
            """
            {"id":"c0e3efae-863c-4068-b0af-69061786aff5","status":"FAILED","failure":"An unexpected error occurred.","failureCode":"INTERNAL.BAD_OUTPUT.CODE01","output":["https://signed.example/video.mp4?token=secret"]}
            """);
        Ok(code01.Status == "FAILED"
            && code01.ProviderFailureCode == "INTERNAL.BAD_OUTPUT.CODE01"
            && code01.FailureText == "An unexpected error occurred."
            && code01.Reason == "INTERNAL.BAD_OUTPUT.CODE01: An unexpected error occurred."
            && code01.ProviderFailureCode != RunwayTaskResponse.InternalFailureLabel
            && RunwayTaskResponse.InternalFailureLabel == ProductionFailureReasons.ProviderUnavailable
            && !code01.DiagnosticJson.Contains("signed.example", StringComparison.Ordinal)
            && !code01.DiagnosticJson.Contains("token", StringComparison.Ordinal),
            "T01 a string failure keeps the provider code and drops the output URL");

        var obj = RunwayTaskResponse.Read(
            """
            {"status":"FAILED","failureCode":"INTERNAL.BAD_OUTPUT.CODE01","failure":{"message":"quality rejected","authorization":"Bearer secret","detail":"frame"}}
            """);
        Ok(obj.FailureText == "quality rejected"
            && obj.Reason == "INTERNAL.BAD_OUTPUT.CODE01: quality rejected"
            && obj.DiagnosticJson.Contains("frame", StringComparison.Ordinal)
            && !obj.DiagnosticJson.Contains("Bearer", StringComparison.Ordinal)
            && !obj.DiagnosticJson.Contains("secret", StringComparison.Ordinal),
            "T02 an object failure keeps its message and drops secrets");

        var ok = RunwayTaskResponse.Read("""{"status":"SUCCEEDED","output":["https://cdn.example/a.mp4"]}""");
        Ok(ok.Status == "SUCCEEDED" && ok.ProviderFailureCode is null && ok.Reason is null
            && !ok.DiagnosticJson.Contains("cdn.example", StringComparison.Ordinal),
            "T03 a success has no provider failure code");

        var nulled = RunwayTaskResponse.Read(
            """
            {"status":"FAILED","failure":null,"failureCode":"INTERNAL.BAD_OUTPUT.CODE01","output":["https://signed.example/v.mp4?token=abc"],"apiKey":"sk-live","authorization":"Bearer live"}
            """);
        Ok(nulled.ProviderFailureCode == "INTERNAL.BAD_OUTPUT.CODE01"
            && nulled.ProviderFailureCode != RunwayTaskResponse.InternalFailureLabel
            && nulled.FailureText is null
            && nulled.Reason == "INTERNAL.BAD_OUTPUT.CODE01"
            && Clean(nulled),
            "T04 a null failure keeps the provider code and drops urls and credentials");

        var missingFailure = RunwayTaskResponse.Read(
            """
            {"status":"FAILED","failureCode":"INTERNAL.BAD_OUTPUT.CODE01","output":["https://signed.example/v.mp4?signature=abc"],"password":"pw","credential":"cred"}
            """);
        Ok(missingFailure.ProviderFailureCode == "INTERNAL.BAD_OUTPUT.CODE01"
            && missingFailure.FailureText is null
            && missingFailure.Reason == "INTERNAL.BAD_OUTPUT.CODE01"
            && Clean(missingFailure),
            "T05 a missing failure keeps the provider code and drops urls and credentials");

        var missingCode = RunwayTaskResponse.Read(
            """
            {"status":"FAILED","failure":"An unexpected error occurred.","output":["https://signed.example/v.mp4?token=abc"],"api_key":"sk-live"}
            """);
        Ok(missingCode.ProviderFailureCode is null
            && missingCode.Reason == "An unexpected error occurred."
            && missingCode.Reason != RunwayTaskResponse.InternalFailureLabel
            && Clean(missingCode),
            "T06 a missing failureCode stays empty and does not become PROVIDER_UNAVAILABLE");

        var leaked = RunwayTaskResponse.Read(
            """
            {"status":"FAILED","failureCode":"INTERNAL.BAD_OUTPUT.CODE01","failure":{"message":"quality rejected https://signed.example/v.mp4?token=abc","output":["https://signed.example/v.mp4"],"password":"pw","api_key":"sk","credential":"c","signature":"sig","promptImage":"data:image/jpeg;base64,AAAA","note":"Bearer live-token"}}
            """);
        Ok(leaked.ProviderFailureCode == "INTERNAL.BAD_OUTPUT.CODE01"
            && leaked.FailureText == "quality rejected"
            && leaked.Reason == "INTERNAL.BAD_OUTPUT.CODE01: quality rejected"
            && leaked.Reason != RunwayTaskResponse.InternalFailureLabel
            && Clean(leaked)
            && leaked.DiagnosticJson.Contains("quality rejected", StringComparison.Ordinal),
            "T07 an object failure drops signed urls, data URIs, and secret fields");

        return fail;

        static bool Clean(RunwayTaskRead read)
        {
            var bag = (read.DiagnosticJson + "\n" + read.Reason + "\n" + read.FailureText).ToLowerInvariant();
            return !bag.Contains("signed.example", StringComparison.Ordinal)
                && !bag.Contains("cdn.example", StringComparison.Ordinal)
                && !bag.Contains("token", StringComparison.Ordinal)
                && !bag.Contains("bearer", StringComparison.Ordinal)
                && !bag.Contains("sk-live", StringComparison.Ordinal)
                && !bag.Contains("data:", StringComparison.Ordinal)
                && !bag.Contains("password", StringComparison.Ordinal)
                && !bag.Contains("credential", StringComparison.Ordinal)
                && !bag.Contains("signature", StringComparison.Ordinal)
                && !bag.Contains("apikey", StringComparison.Ordinal)
                && !bag.Contains("api_key", StringComparison.Ordinal);
        }
    }
}
