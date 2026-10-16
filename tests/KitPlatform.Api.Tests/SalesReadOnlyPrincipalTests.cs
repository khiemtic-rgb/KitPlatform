using System.Security.Claims;
using KitPlatform.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class SalesReadOnlyPrincipalTests
{
    [Fact]
    public async Task Sales_read_only_principal_may_analyze_and_may_not_save_or_read_reports()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddSalesAuthorization();
            options.AddReportsAuthorization();
        });
        await using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();

        var readOnly = Principal("sales.read");
        Assert.True((await auth.AuthorizeAsync(readOnly, SalesPolicies.Read)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(readOnly, SalesPolicies.Pos)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(readOnly, SalesPolicies.Write)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(readOnly, ReportsPolicies.Read)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(readOnly, ReportsPolicies.Export)).Succeeded);

        var counter = Principal("sales.read", "sales.pos");
        Assert.True((await auth.AuthorizeAsync(counter, SalesPolicies.Read)).Succeeded);
        Assert.True((await auth.AuthorizeAsync(counter, SalesPolicies.Pos)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(counter, ReportsPolicies.Read)).Succeeded);

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.False((await auth.AuthorizeAsync(anonymous, SalesPolicies.Read)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(anonymous, SalesPolicies.Pos)).Succeeded);
    }

    private static ClaimsPrincipal Principal(params string[] permissions)
    {
        var claims = permissions.Select(permission => new Claim("permission", permission));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
