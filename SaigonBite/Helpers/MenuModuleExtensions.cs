using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.Validators;

namespace SaigonBite.Helpers;

public static class MenuModuleExtensions
{
    /// <summary>
    /// Registers everything UC3 (Manage Menu and Brands, Hub Utilization Report R2) and the Admin
    /// Hubs and Capacity page (FR17) need.
    /// Program.cs calls it once: builder.Services.AddMenuModule();
    /// </summary>
    public static IServiceCollection AddMenuModule(this IServiceCollection services)
    {
        services.AddSingleton<IMenuValidator, MenuValidator>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<IMenuItemRepository, MenuItemRepository>();
        services.AddScoped<IHubRepository, HubRepository>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddSingleton<IHubValidator, HubValidator>();
        services.AddScoped<IHubService, HubService>();
        return services;
    }
}
