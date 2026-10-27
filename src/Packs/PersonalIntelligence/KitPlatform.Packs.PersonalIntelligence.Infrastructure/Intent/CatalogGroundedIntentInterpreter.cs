using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.PersonalIntelligence.Intent;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intent;

public sealed class CatalogGroundedIntentInterpreter : IBusinessIntentInterpreter
{
    readonly HttpClient _http;
    readonly BusinessIntentModelOptions _options;

    public CatalogGroundedIntentInterpreter(HttpClient http, IOptions<BusinessIntentModelOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<InterpretationOutcome> InterpretAsync(
        string utterance,
        IReadOnlyList<CapabilityDescriptor> catalog,
        InterpretationContext context,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
            return new InterpretationOutcome.Unavailable("Language model is not configured. No keyword fallback is used.");

        var prompt = BusinessIntentPrompt.Build(catalog, context);
        var payload = JsonSerializer.Serialize(new
        {
            model = _options.Model.Trim(),
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = utterance },
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(_options.BaseUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new InterpretationOutcome.Unreadable("model_unreachable");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return new InterpretationOutcome.Unreadable("model_http_status_" + (int)response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length > 200_000)
                return new InterpretationOutcome.Unreadable("model_response_shape");

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    return new InterpretationOutcome.Unreadable("model_response_shape");
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(content) || content.Length > 20_000)
                    return new InterpretationOutcome.Unreadable("model_response_shape");
                return Parse(StripFence(content));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return new InterpretationOutcome.Unreadable("model_response_shape");
            }
        }
    }

    public static InterpretationOutcome Parse(string content)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return new InterpretationOutcome.Unreadable("model_json");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new InterpretationOutcome.Unreadable("model_json");

            var root = doc.RootElement;
            var goal = Text(root, "goal");
            if (string.IsNullOrWhiteSpace(goal))
                return new InterpretationOutcome.Unreadable("model_json");

            return new InterpretationOutcome.Ready(new IntentInterpretation(
                goal,
                Texts(root, "completionConditions"),
                Texts(root, "contextUsed"),
                Missing(root),
                Decisions(root),
                Bindings(root),
                Flag(root, "ambiguous"),
                BlankToNull(Text(root, "ambiguityNote")),
                BlankToNull(Text(root, "limit")),
                BlankToNull(Text(root, "nextStep")),
                BlankToNull(Text(root, "requestedTenant"))));
        }
    }

    static IReadOnlyList<IntentBinding> Bindings(JsonElement root)
    {
        if (!root.TryGetProperty("bindings", out var node))
            return [];
        var items = node.ValueKind switch
        {
            JsonValueKind.Object => new[] { node },
            JsonValueKind.Array => node.EnumerateArray().ToArray(),
            _ => [],
        };
        var bindings = new List<IntentBinding>();
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = Text(item, "capabilityId");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            bindings.Add(new IntentBinding(id, SlotMap(item), Text(item, "purpose") ?? ""));
        }

        return bindings;
    }

    static Dictionary<string, string> SlotMap(JsonElement binding)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!binding.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Object)
            return result;
        foreach (var pair in slots.EnumerateObject())
        {
            var value = pair.Value.ValueKind switch
            {
                JsonValueKind.String => pair.Value.GetString(),
                JsonValueKind.Number => pair.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(value))
                result[pair.Name] = value.Trim();
        }

        return result;
    }

    static IReadOnlyList<MissingInformation> Missing(JsonElement root)
    {
        if (!root.TryGetProperty("missingInformation", out var node))
            return [];
        if (node.ValueKind == JsonValueKind.String)
        {
            var text = node.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? [] : [new MissingInformation(text, "")];
        }

        if (node.ValueKind != JsonValueKind.Array)
            return [];
        var items = new List<MissingInformation>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                items.Add(new MissingInformation(item.GetString()!.Trim(), ""));
            else if (item.ValueKind == JsonValueKind.Object)
            {
                var slot = Text(item, "slot");
                if (!string.IsNullOrWhiteSpace(slot))
                    items.Add(new MissingInformation(slot, Text(item, "why") ?? ""));
            }
        }

        return items;
    }

    static IReadOnlyList<HumanDecision> Decisions(JsonElement root)
    {
        if (!root.TryGetProperty("humanDecisions", out var node))
            return [];
        if (node.ValueKind == JsonValueKind.String)
        {
            var text = node.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? [] : [new HumanDecision("", text)];
        }

        if (node.ValueKind != JsonValueKind.Array)
            return [];
        var items = new List<HumanDecision>();
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                items.Add(new HumanDecision("", item.GetString()!.Trim()));
            else if (item.ValueKind == JsonValueKind.Object)
            {
                var why = Text(item, "why");
                if (!string.IsNullOrWhiteSpace(why))
                    items.Add(new HumanDecision(Text(item, "capabilityId") ?? "", why));
            }
        }

        return items;
    }

    static string[] Texts(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node))
            return [];
        if (node.ValueKind == JsonValueKind.String)
        {
            var text = node.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        if (node.ValueKind != JsonValueKind.Array)
            return [];
        return node.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : Text(item, "text"))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToArray();
    }

    static bool Flag(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node))
            return false;
        return node.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(node.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node))
            return null;
        var text = node.ValueKind switch
        {
            JsonValueKind.String => node.GetString(),
            JsonValueKind.Number => node.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    static string StripFence(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
            return text;
        var first = text.IndexOf('\n', StringComparison.Ordinal);
        var last = text.LastIndexOf("```", StringComparison.Ordinal);
        if (first < 0 || last <= first)
            return text;
        return text[(first + 1)..last].Trim();
    }

    static Uri Endpoint(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return new Uri(trimmed);
        return new Uri(trimmed + "/chat/completions");
    }

}
