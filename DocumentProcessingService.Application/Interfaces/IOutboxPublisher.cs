namespace DocumentProcessingService.Application.Interfaces
{
    public sealed record PublishCycleResult(int Fetched, int Published, int Failed);

    public interface IOutboxPublisher
    {
        Task<PublishCycleResult> PublishPendingMessagesAsync(CancellationToken cancellationToken);
    }
}
