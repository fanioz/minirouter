namespace MiniRouter.Services;

public class LogRetentionService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<LogRetentionService> _logger;

    public LogRetentionService(IServiceProvider services, IConfiguration config, ILogger<LogRetentionService> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the app some time to start up and initialize DB
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int retentionDays = _config.GetValue<int>("LOG_RETENTION_DAYS", 30);
                
                // Need to resolve a scoped or singleton ILogService 
                // Since ILogService is singleton, we can just resolve it
                using var scope = _services.CreateScope();
                var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                
                await logService.PruneLogsAsync(retentionDays);
                _logger.LogInformation($"Pruned logs older than {retentionDays} days.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prune logs.");
            }

            // Wait 1 hour before next prune
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
