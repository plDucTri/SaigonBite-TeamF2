using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.Services.Payments;

namespace SaigonBite.Helpers;

public static class OrderModuleExtensions
{
    /// <summary>
    /// Registers everything UC1 (Place and Manage Orders) needs. Program.cs calls it once:
    /// builder.Services.AddOrderModule();  It expects TimeProvider to be registered already (AccountService uses it too)
    /// and Program.cs must call app.UseSession() because the cart is kept in the session.
    /// </summary>
    public static IServiceCollection AddOrderModule(this IServiceCollection services)
    {
        services.AddSingleton<IHeavyRainFlag, HeavyRainFlag>();
        services.AddSingleton<IGatewaySimulator, GatewaySimulator>();

        // Business rules (no database)
        services.AddSingleton<IOrderValidator, OrderValidator>();
        services.AddSingleton<IPricingEngine, PricingEngine>();

        // Strategy pattern: the factory lists the five payment classes (see PaymentStrategyFactory)
        services.AddSingleton<IPaymentStrategyFactory>(sp => new PaymentStrategyFactory(sp.GetRequiredService<IGatewaySimulator>()));
        services.AddSingleton<IPaymentService, PaymentService>();

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderCatalogRepository, OrderCatalogRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IOrderService, OrderService>();

        // A-01: automatic shipper assignment after 5 minutes
        services.AddHostedService<ShipperAutoAssigner>();

        // The cart is kept in the session
        services.AddDistributedMemoryCache();
        services.AddSession(o =>
        {
            o.IdleTimeout = TimeSpan.FromHours(2);
            o.Cookie.HttpOnly = true;
            o.Cookie.IsEssential = true;
        });
        return services;
    }
}
