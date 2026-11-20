using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using KitPlatform.Infrastructure.Data;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class PharmacyKnowledgeExperienceApiTests : IClassFixture<KnowledgeExperienceFactory>
{
    private const string PharmacyTenant = "11111111-1111-4111-8111-111111111111";
    private const string MarketingTenant = "22222222-2222-4222-8222-222222222222";
    private const string PharmacyAdmin = "11111111-1111-4111-8111-1111111111a1";
    private const string PharmacyStaff = "11111111-1111-4111-8111-1111111111a2";
    private const string PharmacyApprover = "11111111-1111-4111-8111-1111111111a3";
    private const string MarketingAdmin = "22222222-2222-4222-8222-2222222222a2";
    private const string PlatformUser = "22222222-2222-4222-8222-2222222222a4";
    private const string OtherTenant = "44444444-4444-4444-8444-444444444444";
    private const string LocalPassword = "Kx-local-only-1";

    private readonly KnowledgeExperienceFactory _factory;
    private readonly ITestOutputHelper _output;

    public PharmacyKnowledgeExperienceApiTests(KnowledgeExperienceFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Pharmacy_marketing_staff_and_platform_stay_separated()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var connection = configuration.GetConnectionString("Default") ?? "";
        Assert.Contains("Port=5436", connection, StringComparison.Ordinal);
        Assert.DoesNotContain("5432", connection, StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await using var conn = await db.CreateOpenConnectionAsync();
        await using (var portCommand = conn.CreateCommand())
        {
            portCommand.CommandText = "SELECT inet_server_port()";
            var port = Convert.ToInt32(await portCommand.ExecuteScalarAsync());
            Assert.Equal(5436, port);
        }

        await using (var seed = conn.CreateCommand())
        {
            seed.CommandText = SeedSql;
            await seed.ExecuteNonQueryAsync();
        }

        var pharmacyAdmin = await Client(PharmacyAdmin, PharmacyTenant);
        var approver = await Client(PharmacyApprover, PharmacyTenant);
        var staff = await Client(PharmacyStaff, PharmacyTenant);
        var marketingAdmin = await Client(MarketingAdmin, MarketingTenant);
        var platform = await Client(PlatformUser, MarketingTenant);

        Assert.Equal(HttpStatusCode.Forbidden, (await pharmacyAdmin.GetAsync("/api/platform/pharmacy-knowledge/versions")).StatusCode);
        var platformCatalog = await platform.GetAsync("/api/platform/pharmacy-knowledge/versions");
        Assert.True(platformCatalog.StatusCode == HttpStatusCode.OK, await platformCatalog.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await marketingAdmin.GetAsync("/api/pharmacy/owned-knowledge")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/pharmacy/owned-knowledge")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await marketingAdmin.GetAsync("/api/pharmacy/owned-knowledge/in-use")).StatusCode);

        var created = await Post(pharmacyAdmin, "/api/pharmacy/owned-knowledge", new
        {
            kind = "GUIDANCE",
            title = "Huong dan quay",
            classification = "Quay",
            body = GuidanceBody(),
        });
        var id = created.GetProperty("id").GetGuid();
        var versionId = created.GetProperty("versions")[0].GetProperty("versionId").GetGuid();
        var updatedAt = created.GetProperty("updatedAt").GetDateTime();
        var reviewed = await Post(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{id}/submit", new
        {
            versionId,
            expectedUpdatedAt = updatedAt,
        });
        var selfApproval = await PostRaw(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{id}/approve", new
        {
            versionId,
            expectedUpdatedAt = reviewed.GetProperty("updatedAt").GetDateTime(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, selfApproval.StatusCode);
        var approved = await Post(approver, $"/api/pharmacy/owned-knowledge/{id}/approve", new
        {
            versionId,
            expectedUpdatedAt = reviewed.GetProperty("updatedAt").GetDateTime(),
        });
        Assert.Equal("APPROVED", approved.GetProperty("lifecycleStatus").GetString());
        var active = await Post(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{id}/activate", new
        {
            versionId,
            expectedUpdatedAt = approved.GetProperty("updatedAt").GetDateTime(),
        });
        Assert.Equal("ACTIVE", active.GetProperty("activations")[0].GetProperty("status").GetString());

        var staffBefore = await GetArray(staff, "/api/pharmacy/owned-knowledge/in-use");
        Assert.DoesNotContain(staffBefore.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.OK, (await PostRaw(pharmacyAdmin, "/api/pharmacy/owned-knowledge/grants", new
        {
            userId = PharmacyStaff,
            action = "read",
            kind = "GUIDANCE",
        })).StatusCode);
        var staffAfter = await GetArray(staff, "/api/pharmacy/owned-knowledge/in-use");
        Assert.Contains(staffAfter.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/pharmacy/owned-knowledge/in-use/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await marketingAdmin.GetAsync($"/api/pharmacy/owned-knowledge/in-use/{id}")).StatusCode);

        var clinical = await Post(pharmacyAdmin, "/api/pharmacy/owned-knowledge", new
        {
            kind = "CLINICAL_KNOWLEDGE",
            title = "Ho so lam sang",
            body = ClinicalBody(),
        });
        var clinicalId = clinical.GetProperty("id").GetGuid();
        var clinicalVersion = clinical.GetProperty("versions")[0].GetProperty("versionId").GetGuid();
        var clinicalReview = await Post(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{clinicalId}/submit", new
        {
            versionId = clinicalVersion,
            expectedUpdatedAt = clinical.GetProperty("updatedAt").GetDateTime(),
        });
        await Post(approver, $"/api/pharmacy/owned-knowledge/{clinicalId}/approve", new
        {
            versionId = clinicalVersion,
            expectedUpdatedAt = clinicalReview.GetProperty("updatedAt").GetDateTime(),
        });
        var clinicalActivate = await PostRaw(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{clinicalId}/activate", new
        {
            versionId = clinicalVersion,
            expectedUpdatedAt = clinicalReview.GetProperty("updatedAt").GetDateTime(),
        });
        Assert.Equal(HttpStatusCode.Forbidden, clinicalActivate.StatusCode);
        var staffCatalog = await GetArray(staff, "/api/pharmacy/owned-knowledge/in-use");
        Assert.DoesNotContain(staffCatalog.EnumerateArray(), item => item.GetProperty("kind").GetString() == "CLINICAL_KNOWLEDGE");

        var umbrella = await PostRaw(pharmacyAdmin, "/api/pharmacy/owned-knowledge/grants", new
        {
            userId = PharmacyStaff,
            action = "clinical",
            kind = "CLINICAL_KNOWLEDGE",
        });
        Assert.Equal(HttpStatusCode.BadRequest, umbrella.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostRaw(pharmacyAdmin, "/api/pharmacy/owned-knowledge/grants", new
        {
            userId = PharmacyStaff,
            action = "draft",
            kind = "CLINICAL_KNOWLEDGE",
        })).StatusCode);
        var staffClinical = await Post(staff, "/api/pharmacy/owned-knowledge", new
        {
            kind = "CLINICAL_KNOWLEDGE",
            title = "Nhan vien soan",
            body = ClinicalBody(),
        });
        var deniedSubmit = await PostRaw(staff, $"/api/pharmacy/owned-knowledge/{staffClinical.GetProperty("id").GetGuid()}/submit", new
        {
            versionId = staffClinical.GetProperty("versions")[0].GetProperty("versionId").GetGuid(),
            expectedUpdatedAt = staffClinical.GetProperty("updatedAt").GetDateTime(),
        });
        Assert.Equal(HttpStatusCode.Forbidden, deniedSubmit.StatusCode);

        var second = await Post(pharmacyAdmin, "/api/pharmacy/owned-knowledge", new
        {
            kind = "OPERATING_PROCEDURE",
            title = "Quy trinh song song",
            body = ProcedureBody(),
        });
        var secondId = second.GetProperty("id").GetGuid();
        var secondVersion = second.GetProperty("versions")[0].GetProperty("versionId").GetGuid();
        var secondReview = await Post(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{secondId}/submit", new
        {
            versionId = secondVersion,
            expectedUpdatedAt = second.GetProperty("updatedAt").GetDateTime(),
        });
        var secondApproved = await Post(approver, $"/api/pharmacy/owned-knowledge/{secondId}/approve", new
        {
            versionId = secondVersion,
            expectedUpdatedAt = secondReview.GetProperty("updatedAt").GetDateTime(),
        });
        var stamp = secondApproved.GetProperty("updatedAt").GetDateTime();
        var left = PostRaw(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{secondId}/activate", new { versionId = secondVersion, expectedUpdatedAt = stamp });
        var right = PostRaw(pharmacyAdmin, $"/api/pharmacy/owned-knowledge/{secondId}/activate", new { versionId = secondVersion, expectedUpdatedAt = stamp });
        var pair = await Task.WhenAll(left, right);
        Assert.Contains(pair, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(pair, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_sessions_authorize_menu_routes_and_tenants()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var connection = configuration.GetConnectionString("Default") ?? "";
        Assert.Contains("Port=5436", connection, StringComparison.Ordinal);
        Assert.DoesNotContain("5432", connection, StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await using var conn = await db.CreateOpenConnectionAsync();
        await using (var portCommand = conn.CreateCommand())
        {
            portCommand.CommandText = "SELECT inet_server_port()";
            Assert.Equal(5436, Convert.ToInt32(await portCommand.ExecuteScalarAsync()));
        }

        await using (var seed = conn.CreateCommand())
        {
            seed.CommandText = SeedSql + SessionSeedSql;
            await seed.ExecuteNonQueryAsync();
        }

        await using (var hashCommand = conn.CreateCommand())
        {
            hashCommand.CommandText = """
                UPDATE users SET password_hash = @hash WHERE username LIKE 'kx-%';
                UPDATE kit_accounts SET password_hash = @hash WHERE email = 'kx-switch@example.test';
                """;
            var parameter = hashCommand.CreateParameter();
            parameter.ParameterName = "hash";
            parameter.Value = BCrypt.Net.BCrypt.HashPassword(LocalPassword, workFactor: 4);
            hashCommand.Parameters.Add(parameter);
            await hashCommand.ExecuteNonQueryAsync();
        }

        var pharm = await SignIn("KX_PHARM", "kx-pharm-admin");
        var staff = await SignIn("KX_PHARM", "kx-pharm-staff");
        var marketing = await SignIn("KX_MKT", "kx-mkt-admin");
        var platform = await SignIn("KX_MKT", "kx-platform");
        var reviewer = await SignIn("KX_MKT", "kx-reviewer");
        var verifier = await SignIn("KX_MKT", "kx-verifier");
        var pinner = await SignIn("KX_PHARM", "kx-pinner");
        var delegateUser = await SignIn("KX_PHARM", "kx-delegate");
        var unknown = await SignIn("KX_UNKNOWN", "kx-unknown-admin");
        var unset = await SignIn("KX_UNSET", "kx-unset-admin");
        var other = await SignIn("KX_OTHER", "kx-other-admin");

        var refreshed = _factory.CreateClient();
        var refresh = await PostRaw(refreshed, "/api/auth/refresh", new { refreshToken = pharm.RefreshToken });
        var refreshText = await refresh.Content.ReadAsStringAsync();
        Assert.True(refresh.StatusCode == HttpStatusCode.OK, refreshText);
        refreshed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            StringProp(JsonDocument.Parse(refreshText).RootElement, "accessToken"));
        await AssertCode(refreshed, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.OK);

        var pinBody = new { status = "APPROVED", referenceSource = "fixture", version = "1" };
        await AssertCatalog(pharm.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(staff.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(marketing.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(platform.Client, read: true, review: false, verify: false, publish: true);
        await AssertCatalog(reviewer.Client, read: true, review: true, verify: false, publish: false);
        await AssertCatalog(verifier.Client, read: true, review: false, verify: true, publish: false);
        await AssertCatalog(pinner.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(delegateUser.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(unknown.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(unset.Client, read: false, review: false, verify: false, publish: false);
        await AssertCatalog(other.Client, read: false, review: false, verify: false, publish: false);

        await AssertCode(pharm.Client, HttpMethod.Get, "/api/pharmacy/knowledge-rules", HttpStatusCode.OK);
        await AssertCode(pinner.Client, HttpMethod.Get, "/api/pharmacy/knowledge-rules", HttpStatusCode.OK);
        await AssertCode(unset.Client, HttpMethod.Get, "/api/pharmacy/knowledge-rules", HttpStatusCode.OK);
        await AssertCode(other.Client, HttpMethod.Get, "/api/pharmacy/knowledge-rules", HttpStatusCode.OK);
        foreach (var denied in new[] { staff.Client, marketing.Client, platform.Client, reviewer.Client, verifier.Client, delegateUser.Client, unknown.Client })
        {
            await AssertCode(denied, HttpMethod.Get, "/api/pharmacy/knowledge-rules", HttpStatusCode.Forbidden);
            await AssertCode(denied, HttpMethod.Post, "/api/pharmacy/knowledge-rules/KX_NONE/approval", HttpStatusCode.Forbidden, pinBody);
        }

        using (var emptyPins = await GetDocument(pharm.Client, "/api/pharmacy/knowledge-rules"))
        {
            Assert.Equal(0, ArrayProp(emptyPins.RootElement, "publishedVersions").GetArrayLength());
            Assert.Equal(0, ArrayProp(emptyPins.RootElement, "adoptions").GetArrayLength());
        }
        await AssertCode(pharm.Client, HttpMethod.Post, "/api/pharmacy/knowledge-rules/KX_NONE/approval", HttpStatusCode.BadRequest, pinBody);
        await AssertCode(pinner.Client, HttpMethod.Post, "/api/pharmacy/knowledge-rules/KX_NONE/approval", HttpStatusCode.BadRequest, pinBody);

        await AssertCode(marketing.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(marketing.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge/in-use", HttpStatusCode.Forbidden);
        await AssertCode(unknown.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(unknown.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge/in-use", HttpStatusCode.Forbidden);
        await AssertCode(staff.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(pinner.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(delegateUser.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(unset.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.OK);
        await AssertCode(other.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.OK);

        var created = await Post(pharm.Client, "/api/pharmacy/owned-knowledge", new
        {
            kind = "GUIDANCE",
            title = "KX isolation marker",
            classification = "Quay",
            body = GuidanceBody(),
        });
        var markerId = created.GetProperty("id").GetGuid();
        var otherList = await GetArray(other.Client, "/api/pharmacy/owned-knowledge");
        Assert.DoesNotContain(otherList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == markerId);
        await AssertCode(other.Client, HttpMethod.Get, $"/api/pharmacy/owned-knowledge/{markerId}", HttpStatusCode.NotFound);
        await AssertCode(staff.Client, HttpMethod.Get, $"/api/pharmacy/owned-knowledge/{markerId}", HttpStatusCode.Forbidden);
        await AssertCode(marketing.Client, HttpMethod.Get, $"/api/pharmacy/owned-knowledge/{markerId}", HttpStatusCode.Forbidden);

        var mismatched = _factory.CreateClient();
        mismatched.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Token(PharmacyAdmin, OtherTenant, "KX_OTHER"));
        await AssertCode(mismatched, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        var mismatchedUse = await mismatched.GetAsync("/api/pharmacy/owned-knowledge/in-use");
        if (mismatchedUse.StatusCode == HttpStatusCode.OK)
        {
            var rows = JsonDocument.Parse(await mismatchedUse.Content.ReadAsStringAsync()).RootElement;
            Assert.DoesNotContain(rows.EnumerateArray(), item => item.GetProperty("id").GetGuid() == markerId);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, mismatchedUse.StatusCode);
        }

        await AssertCode(pharm.Client, HttpMethod.Post, "/api/pharmacy/owned-knowledge/grants", HttpStatusCode.OK, new
        {
            userId = "11111111-1111-4111-8111-1111111111d1",
            action = "draft",
            kind = "GUIDANCE",
        });
        var drafted = await Post(delegateUser.Client, "/api/pharmacy/owned-knowledge", new
        {
            kind = "GUIDANCE",
            title = "Ban nhap duoc cap",
            body = GuidanceBody(),
        });
        await AssertCode(delegateUser.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(delegateUser.Client, HttpMethod.Post, $"/api/pharmacy/owned-knowledge/{drafted.GetProperty("id").GetGuid()}/submit", HttpStatusCode.Forbidden, new
        {
            versionId = drafted.GetProperty("versions")[0].GetProperty("versionId").GetGuid(),
            expectedUpdatedAt = drafted.GetProperty("updatedAt").GetDateTime(),
        });

        var switchPharm = await ChooseWorkspace("KX_PHARM");
        var switchMkt = await ChooseWorkspace("KX_MKT");
        await AssertCode(switchMkt.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.Forbidden);
        await AssertCode(switchPharm.Client, HttpMethod.Get, "/api/pharmacy/owned-knowledge", HttpStatusCode.OK);
        var sessions = new[]
        {
            Menu("pharm-admin", pharm),
            Menu("mkt-admin", marketing),
            Menu("staff", staff),
            Menu("platform", platform),
            Menu("reviewer", reviewer),
            Menu("verifier", verifier),
            Menu("pinner", pinner),
            Menu("delegate", delegateUser),
            Menu("unknown-admin", unknown),
            Menu("unset-admin", unset),
            Menu("other-admin", other),
            Menu("switch-pharm", switchPharm),
            Menu("switch-mkt", switchMkt),
        };
        var menu = JsonSerializer.Serialize(sessions);
        var script = FindRepoFile("client/admin/src/modules/pharmacy/knowledge-session-menu.smoke.ts");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("node", $"--experimental-strip-types \"{script}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        Assert.True(process.Start());
        await process.StandardInput.WriteAsync(menu);
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        _output.WriteLine(stdout);
        Console.WriteLine(stdout);
        Assert.True(process.ExitCode == 0, stdout + stderr);
        Assert.Contains("session-menu ok", stdout, StringComparison.Ordinal);
    }

    private async Task AssertCatalog(HttpClient client, bool read, bool review, bool verify, bool publish)
    {
        var sourceBody = new { status = "ACCEPTED", referenceSource = "fixture" };
        await AssertCode(client, HttpMethod.Get, "/api/platform/pharmacy-knowledge/versions", read ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Get, "/api/platform/pharmacy-knowledge/history", read ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Get, "/api/platform/pharmacy-knowledge/versions/KX_NONE/1", read ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Post, "/api/platform/pharmacy-knowledge/versions/KX_NONE/1/publish", publish ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Post, "/api/platform/pharmacy-knowledge/versions/KX_NONE/1/retire", publish ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Post, "/api/platform/pharmacy-knowledge/versions/KX_NONE/1/submit-review", review ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Post, "/api/platform/pharmacy-knowledge/versions/KX_NONE/1/return-draft", review ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden);
        await AssertCode(client, HttpMethod.Put, "/api/platform/pharmacy-knowledge/versions/KX_NONE/1/source", verify ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, sourceBody);
    }

    private async Task<SignedSession> ChooseWorkspace(string tenantCode)
    {
        var anonymous = _factory.CreateClient();
        var response = await PostRaw(anonymous, "/api/auth/login", new
        {
            username = "kx-switch@example.test",
            password = LocalPassword,
        });
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        using var doc = JsonDocument.Parse(text);
        Assert.True(BoolProp(doc.RootElement, "requiresWorkspaceChoice"));
        var workspaces = ArrayProp(doc.RootElement, "workspaces");
        Assert.Equal(2, workspaces.GetArrayLength());
        var workspace = workspaces.EnumerateArray().Single(item => StringProp(item, "tenantCode") == tenantCode);
        var chosen = _factory.CreateClient();
        var selected = await PostRaw(chosen, "/api/auth/select-workspace", new
        {
            selectionToken = StringProp(doc.RootElement, "selectionToken"),
            userId = StringProp(workspace, "userId"),
        });
        var selectedText = await selected.Content.ReadAsStringAsync();
        Assert.True(selected.StatusCode == HttpStatusCode.OK, selectedText);
        using var selectedDoc = JsonDocument.Parse(selectedText);
        var session = await SessionFromLogin(selectedDoc.RootElement, chosen);
        Assert.Equal(tenantCode, session.TenantCode);
        var listed = await chosen.GetAsync("/api/auth/workspaces");
        var listedText = await listed.Content.ReadAsStringAsync();
        Assert.True(listed.StatusCode == HttpStatusCode.OK, listedText);
        using var listedDoc = JsonDocument.Parse(listedText);
        Assert.Equal(2, listedDoc.RootElement.GetArrayLength());
        return session;
    }

    private async Task<SignedSession> SignIn(string tenantCode, string username)
    {
        var client = _factory.CreateClient();
        var response = await PostRaw(client, "/api/auth/login", new
        {
            username,
            password = LocalPassword,
            tenantCode,
        });
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{username} {text}");
        using var doc = JsonDocument.Parse(text);
        return await SessionFromLogin(doc.RootElement, client);
    }

    private async Task<SignedSession> SessionFromLogin(JsonElement doc, HttpClient client)
    {
        var user = Prop(doc, "user");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StringProp(doc, "accessToken"));
        var settings = await client.GetAsync("/api/system/tenant-platform");
        var settingsText = await settings.Content.ReadAsStringAsync();
        Assert.True(settings.StatusCode == HttpStatusCode.OK, settingsText);
        using var settingsDoc = JsonDocument.Parse(settingsText);
        return new SignedSession(
            client,
            StringProp(user, "tenantCode"),
            Strings(user, "roles"),
            Strings(user, "permissions"),
            StringProp(settingsDoc.RootElement, "vertical"),
            StringProp(doc, "refreshToken"));
    }

    private static object Menu(string name, SignedSession session) => new
    {
        name,
        roles = session.Roles,
        permissions = session.Permissions,
        vertical = session.Vertical,
    };

    private static async Task AssertCode(HttpClient client, HttpMethod method, string path, HttpStatusCode expected, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{method.Method} {path} {(int)response.StatusCode} {text}");
    }

    private static async Task<JsonDocument> GetDocument(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        return JsonDocument.Parse(text);
    }

    private static JsonElement Prop(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var camel))
            return camel;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out var upper))
            return upper;
        throw new InvalidOperationException($"Missing {name}");
    }

    private static string StringProp(JsonElement element, string name) => Prop(element, name).GetString() ?? "";

    private static bool BoolProp(JsonElement element, string name) => Prop(element, name).GetBoolean();

    private static JsonElement ArrayProp(JsonElement element, string name) => Prop(element, name);

    private static string[] Strings(JsonElement element, string name) =>
        Prop(element, name).EnumerateArray().Select(item => item.GetString() ?? "").ToArray();

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(relative);
    }

    private sealed record SignedSession(
        HttpClient Client,
        string TenantCode,
        string[] Roles,
        string[] Permissions,
        string Vertical,
        string RefreshToken);

    private async Task<HttpClient> Client(string userId, string tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId, tenantId));
        return client;
    }

    private static async Task<JsonElement> Post(HttpClient client, string path, object body)
    {
        var response = await PostRaw(client, path, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement;
    }

    private static async Task<HttpResponseMessage> PostRaw(HttpClient client, string path, object body)
    {
        var json = JsonSerializer.Serialize(body);
        return await client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private static async Task<JsonElement> GetArray(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement;
    }

    private static string Token(string userId, string tenantId) =>
        Token(userId, tenantId, tenantId == PharmacyTenant ? "KX_PHARM" : "KX_MKT");

    private static string Token(string userId, string tenantId, string tenantCode)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "KitPlatform-dev-secret-change-in-production-min-32-chars"));
        var token = new JwtSecurityToken(
            issuer: "KitPlatform",
            audience: "KitPlatform.Client",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim("tenant_id", tenantId),
                new Claim("tenant_code", tenantCode),
            ],
            expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GuidanceBody() =>
        """
        ## Mục đích
        Giai thich
        ## Đối tượng áp dụng
        Nhan vien
        ## Nội dung chính
        Lam theo
        ## Giới hạn
        Khong tu y
        """;

    private static string ClinicalBody() =>
        """
        ## Mục đích
        Luu ho so
        ## Nội dung chính
        Khong tu van
        ## Cảnh báo và giới hạn
        Khong ban hang
        """;

    private static string ProcedureBody() =>
        """
        ## Mục đích
        Mo quay
        ## Phạm vi áp dụng
        Ca sang
        ## Các bước thực hiện
        Buoc 1
        ## Người chịu trách nhiệm
        Quan ly
        """;

    private const string SeedSql = """
        CREATE SCHEMA IF NOT EXISTS kit_workspace;
        CREATE TABLE IF NOT EXISTS kit_workspace.workspace_workspace (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL,
            package_code text,
            is_default boolean,
            deleted_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS pharmacy_symptom (id uuid PRIMARY KEY);
        ALTER TABLE pharmacy_symptom ADD COLUMN IF NOT EXISTS code text;
        CREATE TABLE IF NOT EXISTS pharmacy_knowledge_rule_version (
            rule_code text,
            version text,
            lifecycle_status text,
            source text,
            effective_from timestamptz,
            effective_to timestamptz,
            symptom_id uuid,
            category_codes text[],
            keywords text[],
            exclude_keywords text[],
            reason_vi text,
            priority int,
            reference_source text,
            source_identity text,
            source_url text,
            document_ref text,
            issuing_organization text,
            source_published_on date,
            evidence_note text,
            source_verification_status text,
            verified_by text,
            verified_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS tenants (
            id uuid PRIMARY KEY,
            tenant_code text NOT NULL,
            business_vertical text,
            country_code text,
            settings jsonb,
            status int NOT NULL,
            deleted_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS users (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL,
            username text NOT NULL,
            email text NOT NULL,
            password_hash text NOT NULL,
            status int NOT NULL,
            deleted_at timestamptz,
            last_login_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS kit_account_memberships (
            user_id uuid,
            account_id uuid,
            status int
        );
        CREATE TABLE IF NOT EXISTS roles (
            id uuid PRIMARY KEY,
            role_code text NOT NULL
        );
        CREATE TABLE IF NOT EXISTS user_roles (
            user_id uuid NOT NULL,
            role_id uuid NOT NULL
        );
        CREATE TABLE IF NOT EXISTS permissions (
            id uuid PRIMARY KEY,
            permission_code text NOT NULL
        );
        CREATE TABLE IF NOT EXISTS role_permissions (
            role_id uuid NOT NULL,
            permission_id uuid NOT NULL
        );
        INSERT INTO tenants (id, tenant_code, business_vertical, country_code, settings, status)
        VALUES
            ('11111111-1111-4111-8111-111111111111', 'KX_PHARM', 'pharmacy', 'VN', '{"platform":{"vertical":"pharmacy"}}'::jsonb, 1),
            ('22222222-2222-4222-8222-222222222222', 'KX_MKT', 'hybrid', 'VN', '{"platform":{"vertical":"marketing"}}'::jsonb, 1)
        ON CONFLICT (id) DO UPDATE SET settings = EXCLUDED.settings, business_vertical = EXCLUDED.business_vertical, status = 1, deleted_at = NULL;
        INSERT INTO roles (id, role_code) VALUES
            ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1', 'ADMIN'),
            ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2', 'STAFF'),
            ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa3', 'PLATFORM_READER')
        ON CONFLICT (id) DO NOTHING;
        INSERT INTO permissions (id, permission_code) VALUES
            ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1', 'platform.knowledge.publish')
        ON CONFLICT (id) DO NOTHING;
        INSERT INTO role_permissions (role_id, permission_id)
        SELECT 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa3', 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1'
        WHERE NOT EXISTS (
            SELECT 1 FROM role_permissions
            WHERE role_id = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa3'
              AND permission_id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1');
        INSERT INTO users (id, tenant_id, username, email, password_hash, status) VALUES
            ('11111111-1111-4111-8111-1111111111a1', '11111111-1111-4111-8111-111111111111', 'kx-pharm-admin', 'kx-pharm-admin@example.test', 'x', 1),
            ('11111111-1111-4111-8111-1111111111a2', '11111111-1111-4111-8111-111111111111', 'kx-pharm-staff', 'kx-pharm-staff@example.test', 'x', 1),
            ('11111111-1111-4111-8111-1111111111a3', '11111111-1111-4111-8111-111111111111', 'kx-pharm-approver', 'kx-pharm-approver@example.test', 'x', 1),
            ('22222222-2222-4222-8222-2222222222a2', '22222222-2222-4222-8222-222222222222', 'kx-mkt-admin', 'kx-mkt-admin@example.test', 'x', 1),
            ('22222222-2222-4222-8222-2222222222a4', '22222222-2222-4222-8222-222222222222', 'kx-platform', 'kx-platform@example.test', 'x', 1)
        ON CONFLICT (id) DO UPDATE SET status = 1, deleted_at = NULL, tenant_id = EXCLUDED.tenant_id;
        DELETE FROM pharmacy_owned_knowledge_grant
        WHERE user_id = '11111111-1111-4111-8111-1111111111a2';
        DELETE FROM user_roles WHERE user_id IN (
            '11111111-1111-4111-8111-1111111111a1',
            '11111111-1111-4111-8111-1111111111a2',
            '11111111-1111-4111-8111-1111111111a3',
            '22222222-2222-4222-8222-2222222222a2',
            '22222222-2222-4222-8222-2222222222a4');
        INSERT INTO user_roles (user_id, role_id) VALUES
            ('11111111-1111-4111-8111-1111111111a1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('11111111-1111-4111-8111-1111111111a2', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2'),
            ('11111111-1111-4111-8111-1111111111a3', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('22222222-2222-4222-8222-2222222222a2', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('22222222-2222-4222-8222-2222222222a4', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa3');
        """;

    private const string SessionSeedSql = """
        ALTER TABLE tenants ADD COLUMN IF NOT EXISTS tenant_name text;
        UPDATE tenants SET tenant_name = tenant_code WHERE tenant_name IS NULL;
        ALTER TABLE kit_account_memberships ADD COLUMN IF NOT EXISTS tenant_id uuid;
        ALTER TABLE kit_account_memberships ADD COLUMN IF NOT EXISTS product_code text;
        ALTER TABLE kit_account_memberships ADD COLUMN IF NOT EXISTS is_default boolean NOT NULL DEFAULT false;
        CREATE TABLE IF NOT EXISTS refresh_tokens (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id uuid NOT NULL,
            token_hash text NOT NULL,
            expires_at timestamptz NOT NULL,
            revoked_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS kit_accounts (
            id uuid PRIMARY KEY,
            email text NOT NULL UNIQUE,
            password_hash text NOT NULL,
            display_name text,
            status int NOT NULL,
            deleted_at timestamptz
        );
        CREATE TABLE IF NOT EXISTS pharmacy_knowledge_adoption (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id uuid NOT NULL,
            rule_code text NOT NULL,
            rule_version text NOT NULL,
            status text NOT NULL,
            reference_source text,
            approved_by text,
            approved_at timestamptz
        );
        ALTER TABLE pharmacy_knowledge_adoption ADD COLUMN IF NOT EXISTS approved_by text;
        ALTER TABLE pharmacy_knowledge_adoption ADD COLUMN IF NOT EXISTS approved_at timestamptz;
        CREATE TABLE IF NOT EXISTS pharmacy_tenant_knowledge_policy (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            adoption_id uuid NOT NULL,
            rule_version text NOT NULL,
            product_matching_enabled boolean NOT NULL DEFAULT true,
            extra_exclude_keywords text[] NOT NULL DEFAULT '{}',
            priority_override int
        );
        CREATE TABLE IF NOT EXISTS pharmacy_knowledge_publication_event (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            rule_code text NOT NULL,
            rule_version text NOT NULL,
            from_status text,
            to_status text,
            actor_user_id text,
            created_at timestamptz NOT NULL DEFAULT NOW()
        );
        INSERT INTO tenants (id, tenant_code, tenant_name, business_vertical, country_code, settings, status)
        VALUES
            ('33333333-3333-4333-8333-333333333333', 'KX_UNKNOWN', 'KX_UNKNOWN', 'clinic', 'VN', '{"platform":{"vertical":"unknown"}}'::jsonb, 1),
            ('33333333-3333-4333-8333-333333333334', 'KX_UNSET', 'KX_UNSET', NULL, 'VN', '{}'::jsonb, 1),
            ('44444444-4444-4444-8444-444444444444', 'KX_OTHER', 'KX_OTHER', 'pharmacy', 'VN', '{"platform":{"vertical":"pharmacy"}}'::jsonb, 1)
        ON CONFLICT (id) DO UPDATE
        SET settings = EXCLUDED.settings, business_vertical = EXCLUDED.business_vertical, tenant_name = EXCLUDED.tenant_name, status = 1, deleted_at = NULL;
        INSERT INTO roles (id, role_code) VALUES
            ('cccccccc-cccc-4ccc-8ccc-ccccccccccc1', 'KX_REVIEWER'),
            ('cccccccc-cccc-4ccc-8ccc-ccccccccccc2', 'KX_VERIFIER'),
            ('cccccccc-cccc-4ccc-8ccc-ccccccccccc3', 'KX_PINNER')
        ON CONFLICT (id) DO NOTHING;
        INSERT INTO permissions (id, permission_code) VALUES
            ('dddddddd-dddd-4ddd-8ddd-ddddddddddd1', 'pharmacy.knowledge.review'),
            ('dddddddd-dddd-4ddd-8ddd-ddddddddddd2', 'pharmacy.knowledge.verify-source'),
            ('dddddddd-dddd-4ddd-8ddd-ddddddddddd3', 'pharmacy.knowledge.approve')
        ON CONFLICT (id) DO NOTHING;
        INSERT INTO role_permissions (role_id, permission_id)
        SELECT 'cccccccc-cccc-4ccc-8ccc-ccccccccccc1', 'dddddddd-dddd-4ddd-8ddd-ddddddddddd1'
        WHERE NOT EXISTS (
            SELECT 1 FROM role_permissions
            WHERE role_id = 'cccccccc-cccc-4ccc-8ccc-ccccccccccc1'
              AND permission_id = 'dddddddd-dddd-4ddd-8ddd-ddddddddddd1');
        INSERT INTO role_permissions (role_id, permission_id)
        SELECT 'cccccccc-cccc-4ccc-8ccc-ccccccccccc2', 'dddddddd-dddd-4ddd-8ddd-ddddddddddd2'
        WHERE NOT EXISTS (
            SELECT 1 FROM role_permissions
            WHERE role_id = 'cccccccc-cccc-4ccc-8ccc-ccccccccccc2'
              AND permission_id = 'dddddddd-dddd-4ddd-8ddd-ddddddddddd2');
        INSERT INTO role_permissions (role_id, permission_id)
        SELECT 'cccccccc-cccc-4ccc-8ccc-ccccccccccc3', 'dddddddd-dddd-4ddd-8ddd-ddddddddddd3'
        WHERE NOT EXISTS (
            SELECT 1 FROM role_permissions
            WHERE role_id = 'cccccccc-cccc-4ccc-8ccc-ccccccccccc3'
              AND permission_id = 'dddddddd-dddd-4ddd-8ddd-ddddddddddd3');
        INSERT INTO users (id, tenant_id, username, email, password_hash, status) VALUES
            ('22222222-2222-4222-8222-2222222222b1', '22222222-2222-4222-8222-222222222222', 'kx-reviewer', 'kx-reviewer@example.test', 'x', 1),
            ('22222222-2222-4222-8222-2222222222b2', '22222222-2222-4222-8222-222222222222', 'kx-verifier', 'kx-verifier@example.test', 'x', 1),
            ('11111111-1111-4111-8111-1111111111b3', '11111111-1111-4111-8111-111111111111', 'kx-pinner', 'kx-pinner@example.test', 'x', 1),
            ('11111111-1111-4111-8111-1111111111d1', '11111111-1111-4111-8111-111111111111', 'kx-delegate', 'kx-delegate@example.test', 'x', 1),
            ('33333333-3333-4333-8333-3333333333a1', '33333333-3333-4333-8333-333333333333', 'kx-unknown-admin', 'kx-unknown-admin@example.test', 'x', 1),
            ('33333333-3333-4333-8333-3333333333a2', '33333333-3333-4333-8333-333333333334', 'kx-unset-admin', 'kx-unset-admin@example.test', 'x', 1),
            ('44444444-4444-4444-8444-4444444444a1', '44444444-4444-4444-8444-444444444444', 'kx-other-admin', 'kx-other-admin@example.test', 'x', 1),
            ('11111111-1111-4111-8111-1111111111c1', '11111111-1111-4111-8111-111111111111', 'kx-switch-pharm', 'kx-switch-pharm@example.test', 'x', 1),
            ('22222222-2222-4222-8222-2222222222c1', '22222222-2222-4222-8222-222222222222', 'kx-switch-mkt', 'kx-switch-mkt@example.test', 'x', 1)
        ON CONFLICT (id) DO UPDATE SET status = 1, deleted_at = NULL, tenant_id = EXCLUDED.tenant_id, username = EXCLUDED.username;
        DELETE FROM pharmacy_owned_knowledge_grant WHERE user_id = '11111111-1111-4111-8111-1111111111d1';
        DELETE FROM user_roles WHERE user_id IN (
            '22222222-2222-4222-8222-2222222222b1',
            '22222222-2222-4222-8222-2222222222b2',
            '11111111-1111-4111-8111-1111111111b3',
            '11111111-1111-4111-8111-1111111111d1',
            '33333333-3333-4333-8333-3333333333a1',
            '33333333-3333-4333-8333-3333333333a2',
            '44444444-4444-4444-8444-4444444444a1',
            '11111111-1111-4111-8111-1111111111c1',
            '22222222-2222-4222-8222-2222222222c1');
        INSERT INTO user_roles (user_id, role_id) VALUES
            ('22222222-2222-4222-8222-2222222222b1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2'),
            ('22222222-2222-4222-8222-2222222222b1', 'cccccccc-cccc-4ccc-8ccc-ccccccccccc1'),
            ('22222222-2222-4222-8222-2222222222b2', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2'),
            ('22222222-2222-4222-8222-2222222222b2', 'cccccccc-cccc-4ccc-8ccc-ccccccccccc2'),
            ('11111111-1111-4111-8111-1111111111b3', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2'),
            ('11111111-1111-4111-8111-1111111111b3', 'cccccccc-cccc-4ccc-8ccc-ccccccccccc3'),
            ('11111111-1111-4111-8111-1111111111d1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2'),
            ('33333333-3333-4333-8333-3333333333a1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('33333333-3333-4333-8333-3333333333a2', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('44444444-4444-4444-8444-4444444444a1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('11111111-1111-4111-8111-1111111111c1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1'),
            ('22222222-2222-4222-8222-2222222222c1', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1');
        INSERT INTO kit_accounts (id, email, password_hash, display_name, status)
        VALUES ('55555555-5555-4555-8555-555555555551', 'kx-switch@example.test', 'x', 'kx-switch', 1)
        ON CONFLICT (email) DO UPDATE SET status = 1, deleted_at = NULL;
        DELETE FROM kit_account_memberships WHERE user_id IN (
            '11111111-1111-4111-8111-1111111111c1',
            '22222222-2222-4222-8222-2222222222c1');
        INSERT INTO kit_account_memberships (account_id, tenant_id, user_id, product_code, is_default, status) VALUES
            ('55555555-5555-4555-8555-555555555551', '11111111-1111-4111-8111-111111111111', '11111111-1111-4111-8111-1111111111c1', 'pharmacy', true, 1),
            ('55555555-5555-4555-8555-555555555551', '22222222-2222-4222-8222-222222222222', '22222222-2222-4222-8222-2222222222c1', 'hybrid', false, 1);
        """;
}

public sealed class KnowledgeExperienceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:Default",
            KitPlatformWebApplicationFactory.RequireIsolatedConnection());
        builder.ConfigureTestServices(services =>
        {
            var hosted = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList();
            foreach (var descriptor in hosted)
                services.Remove(descriptor);
        });
    }
}
