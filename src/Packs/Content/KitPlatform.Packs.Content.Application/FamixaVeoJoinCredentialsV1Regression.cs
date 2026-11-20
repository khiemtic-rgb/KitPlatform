namespace KitPlatform.Packs.Content;

public static class FamixaVeoJoinCredentialsV1Regression
{
    public const string SuiteId = "FAMIXA_VEO_JOIN_CREDENTIALS_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var missing = FamixaVeoJoinCredentialsV1.Accept(null, null);
        Ok(!missing.Ok && missing.Code == "VEO_JOIN_FILE_MISSING", "T1 missing fail-closed");

        var gemini = FamixaVeoJoinCredentialsV1.Accept("AIzaSyDummyGeminiKeyThatLooksReal12", "key.txt");
        Ok(!gemini.Ok && gemini.Code == "GEMINI_API_KEY_REJECTED", "T2 Gemini AIza rejected");

        var aq = FamixaVeoJoinCredentialsV1.Accept("AQ.Ab8R" + new string('x', 40), "gemini.json");
        Ok(!aq.Ok && aq.Code == "GEMINI_API_KEY_REJECTED", "T3 Gemini AQ. rejected");

        var join = FamixaVeoJoinCredentialsV1.Accept(
            """{"type":"service_account","project_id":"demo","client_email":"veo@demo.iam.gserviceaccount.com"}""",
            "C:\\secrets\\veo-join.json");
        Ok(join.Ok && join.FileName == "veo-join.json" && join.NormalizedText!.Contains("project_id", StringComparison.Ordinal),
            "T4 JSON Join accepted");
        Ok(!join.NormalizedText!.StartsWith("AIza", StringComparison.Ordinal), "T5 accepted Join is not a Gemini key");
        Ok(FamixaVeoJoinCredentialsV1.TryParseAccount(join.NormalizedText) is null, "T6 Join without private_key is not a Vertex account");

        var account = FamixaVeoJoinCredentialsV1.TryParseAccount(
            """{"type":"service_account","project_id":"demo","client_email":"veo@demo.iam.gserviceaccount.com","private_key":"-----BEGIN PRIVATE KEY-----\nMIIB\n-----END PRIVATE KEY-----\n"}""");
        Ok(account is not null
           && account.ProjectId == "demo"
           && account.ClientEmail.StartsWith("veo@", StringComparison.Ordinal)
           && !account.ToString().Contains("BEGIN", StringComparison.Ordinal),
            "T7 service account parse hides PEM");

        return fail;
    }
}
