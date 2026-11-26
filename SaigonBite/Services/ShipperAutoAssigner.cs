namespace SaigonBite.Services;

/// <summary>
/// Runs in the background and calls OrderService.AssignOverdueJobsAsync every 30 seconds (A-01, T-54).
/// It waits one interval before the first run so the database is ready.
/// </summary>
public class ShipperAutoAssigner : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ShipperAutoAssigner> _logger;

    public ShipperAutoAssigner(IServiceScopeFactory scopes, ILogger<ShipperAutoAssigner> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                using var scope = _scopes.CreateScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                int assigned = await orders.AssignOverdueJobsAsync();
                if (assigned > 0)
                    _logger.LogInformation("Automatically assigned {Count} Ready order(s) to shippers.", assigned);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Automatic shipper assignment failed; it will try again.");
            }
        }
    }
}
