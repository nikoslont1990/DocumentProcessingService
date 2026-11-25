namespace DocumentProcessingService.Application.Interfaces
{
    public sealed record PublishCycleResult(int Fetched, int Published, int Failed);

    public sealed record BatchPublishResult(int Published, int Failed);

    public interface IOutboxPublisher
    {
        Task<PublishCycleResult> PublishPendingMessagesAsync(CancellationToken cancellationToken);

        Task<PublishCycleResult> PublishPendingMessagesOneByOne(CancellationToken cancellationToken);
    }
}
