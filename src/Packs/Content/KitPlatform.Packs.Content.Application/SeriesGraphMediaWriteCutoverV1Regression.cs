using System.Text.Json.Nodes;

namespace KitPlatform.Packs.Content;

public static class SeriesGraphMediaWriteCutoverV1Regression
{
    public const string SuiteId = SeriesGraphMediaWriteCutover.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var existing = """
            {
              "scriptLocked": true,
              "episode": { "title": "EP01", "shots": [{ "id": "SH01" }] },
              "runs": {
                "SH01": {
                  "status": "turbo_testing",
                  "takeUrl": "old-take.mp4",
                  "previewUrl": "old-preview.mp4",
                  "lipsyncUrl": "old-lip.mp4",
                  "acceptedTake": { "n": 1, "url": "old-accept.mp4" },
                  "localVideoPath": "E:/old.mp4",
                  "turboTaskId": "turbo-old",
                  "lipsyncTaskId": "lip-old",
                  "runwayAttempts": [{ "n": 1, "outputUrl": "old-out.mp4" }]
                }
              }
            }
            """;

        var incomingOverwrite = """
            {
              "scriptLocked": false,
              "episode": { "title": "EP01-b", "shots": [{ "id": "SH01", "story": "new story" }] },
              "runs": {
                "SH01": {
                  "status": "story_locked",
                  "takeUrl": "new-take.mp4",
                  "previewUrl": "new-preview.mp4",
                  "lipsyncUrl": "new-lip.mp4",
                  "acceptedTake": { "n": 9, "url": "new-accept.mp4" },
                  "localVideoPath": "E:/new.mp4",
                  "turboTaskId": "turbo-new",
                  "lipsyncTaskId": "lip-new",
                  "runwayAttempts": [{ "n": 9, "outputUrl": "new-out.mp4" }]
                }
              }
            }
            """;

        var stored = Parse(SeriesGraphMediaWriteCutover.Apply(existing, incomingOverwrite));
        var run = stored["runs"]!["SH01"]!.AsObject();
        Ok(stored["scriptLocked"]!.GetValue<bool>() == false, "T1 script mutation persists");
        Ok(stored["episode"]!["title"]!.GetValue<string>() == "EP01-b"
            && stored["episode"]!["shots"]![0]!["story"]!.GetValue<string>() == "new story",
            "T9 script episode/story persists");
        Ok(run["status"]!.GetValue<string>() == "story_locked", "T9 run script status persists");
        Ok(run["takeUrl"]!.GetValue<string>() == "old-take.mp4", "T2 takeUrl not overwritten");
        Ok(run["previewUrl"]!.GetValue<string>() == "old-preview.mp4", "T3 previewUrl not overwritten");
        Ok(run["lipsyncUrl"]!.GetValue<string>() == "old-lip.mp4", "T4 lipsyncUrl not overwritten");
        Ok(run["acceptedTake"]!["url"]!.GetValue<string>() == "old-accept.mp4"
            && run["acceptedTake"]!["n"]!.GetValue<int>() == 1, "T5 acceptedTake not overwritten");
        Ok(run["turboTaskId"]!.GetValue<string>() == "turbo-old"
            && run["lipsyncTaskId"]!.GetValue<string>() == "lip-old"
            && run["runwayAttempts"]![0]!["outputUrl"]!.GetValue<string>() == "old-out.mp4",
            "T8 turbo/task/output pointers stay historical");

        var incomingOmit = """
            {
              "scriptLocked": false,
              "runs": { "SH01": { "status": "story_locked" } }
            }
            """;
        var omitted = Parse(SeriesGraphMediaWriteCutover.Apply(existing, incomingOmit))["runs"]!["SH01"]!.AsObject();
        Ok(omitted["takeUrl"]!.GetValue<string>() == "old-take.mp4"
            && omitted["previewUrl"]!.GetValue<string>() == "old-preview.mp4"
            && omitted["lipsyncUrl"]!.GetValue<string>() == "old-lip.mp4"
            && omitted["acceptedTake"]!["url"]!.GetValue<string>() == "old-accept.mp4",
            "T6 omitted media keeps historical");

        var scriptOnly = """
            {
              "scriptLocked": true,
              "lines": [{ "id": "L1", "text": "hello" }],
              "runs": { "SH01": { "status": "story_locked", "shotAction": "turns" } }
            }
            """;
        var afterScript = Parse(SeriesGraphMediaWriteCutover.Apply(existing, scriptOnly));
        Ok(afterScript["scriptLocked"]!.GetValue<bool>()
            && afterScript["lines"]![0]!["text"]!.GetValue<string>() == "hello"
            && afterScript["runs"]!["SH01"]!["shotAction"]!.GetValue<string>() == "turns"
            && afterScript["runs"]!["SH01"]!["takeUrl"]!.GetValue<string>() == "old-take.mp4",
            "T7 historical media remains after script-only save");

        var firstWrite = Parse(SeriesGraphMediaWriteCutover.Apply("{}", incomingOverwrite));
        var fresh = firstWrite["runs"]!["SH01"]!.AsObject();
        Ok(!fresh.ContainsKey("takeUrl")
            && !fresh.ContainsKey("previewUrl")
            && !fresh.ContainsKey("lipsyncUrl")
            && !fresh.ContainsKey("acceptedTake")
            && !fresh.ContainsKey("turboTaskId")
            && !fresh.ContainsKey("runwayAttempts")
            && firstWrite["scriptLocked"]!.GetValue<bool>() == false,
            "T10 / NEGATIVE new media URL cannot become stored graph authority");

        var service = File.ReadAllText(FindPilotService());
        Ok(service.Contains("SeriesGraphMediaWriteCutover.Apply", StringComparison.Ordinal)
            && service.Contains("UpsertBuildAsync", StringComparison.Ordinal)
            && service.Contains("UpsertAsync", StringComparison.Ordinal),
            "service persist uses cutover");

        return fail;
    }

    static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    static string FindPilotService()
    {
        var named = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure",
            "ContentSeriesPilotService.cs");
        if (File.Exists(named))
            return Path.GetFullPath(named);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Directory.EnumerateFiles(root, "ContentSeriesPilotService.cs", SearchOption.AllDirectories)
            .First(p => p.Contains("Infrastructure", StringComparison.Ordinal));
    }
}
