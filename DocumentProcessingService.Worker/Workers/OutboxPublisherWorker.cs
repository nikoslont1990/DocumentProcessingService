using DocumentProcessingService.Application.Interfaces;

namespace DocumentProcessingService.Worker.Workers
{
    public class OutboxPublisherWorker(ILogger<OutboxPublisherWorker> logger, IConfiguration configuration,
        IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pollMinutes = configuration.GetValue<int?>("Outbox:PollMinutes") ?? 2;
            logger.LogInformation("OutboxPublisherWorker started. PollMinutes={PollMinutes}", pollMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishOutboxAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(pollMinutes), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation("Outbox publishing worker is stopping due to cancellation.");
                    break; // Exit the loop if cancellation is requested
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while publishing outbox messages.");
                    await Task.Delay(TimeSpan.FromMinutes(pollMinutes), stoppingToken);
                }
            }
        }

        private async Task PublishOutboxAsync(CancellationToken stoppingToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var publisher = scope.ServiceProvider
                .GetRequiredService<IOutboxPublisher>();

            var result = await publisher.PublishPendingMessagesAsync(stoppingToken);

            logger.LogInformation("Outbox cycle completed. Fetched={Fetched}, Published={Published}, Failed={Failed}",
            result.Fetched, result.Published, result.Failed);
        }
    }
}
