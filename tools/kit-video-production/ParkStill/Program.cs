using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content.Production;
using Npgsql;

const string Model = "gemini-3.1-flash-image";
var repo = Arg(args, "--repo");
var dest = Arg(args, "--out");
var promptPath = Arg(args, "--prompt-file");
var reference = ArgOptional(args, "--reference");
if (repo is null || dest is null || promptPath is null)
{
    Console.Error.WriteLine("FAIL missing arguments");
    return 1;
}

var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "src", "KitPlatform.Api", "appsettings.Development.json")));
var root = settings.RootElement;
var connection = root.GetProperty("ConnectionStrings").GetProperty("Default").GetString() ?? "";
var credentials = root.GetProperty("ProductionCredentials");
var keyId = credentials.GetProperty("DataEncryptionKeyId").GetString();
var keyMaterial = credentials.GetProperty("DataEncryptionKey").GetString();

await using var conn = new NpgsqlConnection(ToNpgsql(connection));
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand(
    """
    SELECT v.secret_key_id, v.secret_nonce, v.secret_ciphertext, v.secret_tag
    FROM pack_content.production_tenant_ai_config c
    JOIN pack_content.production_credential_versions v
      ON v.credential_id = c.credential_id AND v.tenant_id = c.tenant_id
    JOIN pack_content.production_credentials p
      ON p.credential_id = v.credential_id AND p.tenant_id = v.tenant_id
    WHERE c.capability = 'IMAGE' AND c.enabled IS TRUE
      AND upper(c.provider_id) = 'GEMINI' AND c.model_id = @model
      AND p.status = 'ACTIVE' AND v.status = 'ACTIVE'
    """,
    conn);
cmd.Parameters.AddWithValue("model", Model);
await using var reader = await cmd.ExecuteReaderAsync();
var rows = new List<(string KeyId, byte[] Nonce, byte[] Cipher, byte[] Tag)>();
while (await reader.ReadAsync())
{
    rows.Add((reader.GetString(0), (byte[])reader[1], (byte[])reader[2], (byte[])reader[3]));
}
await reader.CloseAsync();
if (rows.Count != 1)
{
    Console.Error.WriteLine("FAIL image row");
    return 1;
}

string secret;
try
{
    var key = ProductionCredentialSecretProtector.ParseKey(keyId, keyMaterial);
    if (!string.Equals(rows[0].KeyId, key.KeyId, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("FAIL key id");
        return 1;
    }
    secret = ProductionCredentialSecretProtector.Decrypt(
        new ProductionCredentialSecretEnvelope(1, rows[0].KeyId, rows[0].Nonce, rows[0].Cipher, rows[0].Tag),
        key);
}
catch (InvalidOperationException)
{
    Console.Error.WriteLine("FAIL credential");
    return 1;
}

var parts = new List<object>();
if (!string.IsNullOrWhiteSpace(reference))
{
    var file = new FileInfo(reference);
    parts.Add(new { text = $"REFERENCE — {file.Name}. Real software screenshot. Edit around it. Do not replace it with a different application." });
    parts.Add(new
    {
        inline_data = new
        {
            mime_type = file.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg",
            data = Convert.ToBase64String(await File.ReadAllBytesAsync(file.FullName)),
        }
    });
}
parts.Add(new { text = await File.ReadAllTextAsync(promptPath) });
var body = JsonSerializer.Serialize(new
{
    contents = new[] { new { role = "user", parts } },
    generationConfig = new
    {
        responseModalities = new[] { "TEXT", "IMAGE" },
        imageConfig = new { aspectRatio = "16:9", imageSize = "512" },
    },
});

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
request.Headers.TryAddWithoutValidation("x-goog-api-key", secret.Trim());
request.Content = new StringContent(body, Encoding.UTF8, "application/json");
using var response = await http.SendAsync(request);
var raw = await response.Content.ReadAsStringAsync();
if (!response.IsSuccessStatusCode)
{
    var snippet = raw.Length > 300 ? raw[..300] : raw;
    Console.Error.WriteLine($"FAIL http {(int)response.StatusCode} {snippet}");
    return 1;
}
var image = Extract(raw);
if (image is null || image.Length < 32)
{
    Console.Error.WriteLine("FAIL no image");
    return 1;
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dest))!);
await File.WriteAllBytesAsync(dest, image);
Console.WriteLine("OK " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image)).ToLowerInvariant());
return 0;

static string? Arg(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string? ArgOptional(string[] args, string name) => Arg(args, name);

static string ToNpgsql(string ado)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in ado.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var cut = item.IndexOf('=');
        if (cut <= 0) continue;
        map[item[..cut].Trim()] = item[(cut + 1)..].Trim();
    }
    return $"Host={map["Host"]};Port={map.GetValueOrDefault("Port", "5432")};Database={map["Database"]};Username={map["Username"]};Password={map["Password"]}";
}

static byte[]? Extract(string json)
{
    using var doc = JsonDocument.Parse(json);
    if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        return null;
    if (!candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
        return null;
    byte[]? visible = null;
    byte[]? thought = null;
    foreach (var part in parts.EnumerateArray())
    {
        if (!part.TryGetProperty("inlineData", out var inline) && !part.TryGetProperty("inline_data", out inline))
            continue;
        if (!inline.TryGetProperty("data", out var data) || data.GetString() is not { Length: > 0 } encoded)
            continue;
        var bytes = Convert.FromBase64String(encoded);
        var isThought = part.TryGetProperty("thought", out var flag) && flag.ValueKind == JsonValueKind.True;
        if (isThought) thought = bytes;
        else visible = bytes;
    }
    return visible ?? thought;
}
