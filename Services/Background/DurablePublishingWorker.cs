using Social_Media_Studio.Services.Interfaces;

namespace Social_Media_Studio.Services.Background;

public class DurablePublishingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DurablePublishingWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public DurablePublishingWorker(IServiceScopeFactory scopeFactory, ILogger<DurablePublishingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DurablePublishingWorker started. Monitoring scheduled slots every {Interval}s...", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var schedulingService = scope.ServiceProvider.GetRequiredService<ISchedulingService>();

                int processed = await schedulingService.ProcessDueSlotsAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("DurablePublishingWorker successfully processed {Count} due slots.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DurablePublishingWorker execution cycle.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("DurablePublishingWorker stopped.");
    }
}
