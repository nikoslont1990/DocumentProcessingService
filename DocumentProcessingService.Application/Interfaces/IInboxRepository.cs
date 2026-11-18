namespace DocumentProcessingService.Application.Interfaces
{
    public interface IInboxRepository
    {
        Task<bool> AlreadyExists(string messageId, CancellationToken cancellationToken);
        Task AddMessageAsync(string messageId, string correlationId, string consumer, CancellationToken cancellationToken);
    }
}
