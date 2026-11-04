using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KitPlatform.Api.Tests;

/// <summary>
/// API host for the controlled-pilot proof. Background workers stay off so a local sale
/// cannot publish Local OS or dispatch national drug stock-out sync.
/// Same isolated-database gate as <see cref="KitPlatformWebApplicationFactory"/>: these tests write orders,
/// shifts and the tenant batch mode.
/// </summary>
public sealed class ControlledPilotHostFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var connection = KitPlatformWebApplicationFactory.RequireIsolatedConnection();
        builder.UseSetting("ConnectionStrings:Default", connection);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connection,
                ["NationalDrugCatalog:Mode"] = "mock",
                ["NationalDrugCatalog:EnableStockOutSync"] = "false",
                ["NationalDrugCatalog:Username"] = "",
                ["NationalDrugCatalog:Password"] = "",
            });
        });
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(descriptor);
        });
    }
}
