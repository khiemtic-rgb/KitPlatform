using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

public static class ConnectPackDependencyInjection
{
    public static IServiceCollection AddConnectPack(this IServiceCollection services)
    {
        services.AddScoped<IConnectOverviewService, ConnectOverviewService>();
        services.AddScoped<ConnectOrgProfileRepository>();
        services.AddScoped<IConnectOrgProfileService, ConnectOrgProfileService>();
        services.AddScoped<ConnectOrgLinkRepository>();
        services.AddScoped<IConnectOrgLinkService, ConnectOrgLinkService>();
        services.AddScoped<ConnectDoctorMembershipRepository>();
        services.AddScoped<IConnectDoctorMembershipService, ConnectDoctorMembershipService>();
        services.AddScoped<ConnectReferralRepository>();
        services.AddScoped<IConnectReferralService, ConnectReferralService>();
        services.AddScoped<IConnectNotifyService, ConnectNotifyService>();
        services.AddScoped<ConnectBookingRepository>();
        services.AddScoped<IConnectBookingService, ConnectBookingService>();
        services.AddScoped<IConnectClinicCalendarBridge, NullConnectClinicCalendarBridge>();
        services.AddScoped<ConnectStatusEventRepository>();
        services.AddScoped<IConnectStatusEventService, ConnectStatusEventService>();
        services.AddScoped<ConnectRxHandoffRepository>();
        services.AddScoped<IConnectRxHandoffService, ConnectRxHandoffService>();
        services.AddScoped<IConnectPartnerCatalogService, ConnectPartnerCatalogService>();
        return services;
    }
}
