namespace DocumentProcessingService.Application.Interfaces
{
    public interface IInboxRepository
    {
        Task<bool> IsDuplicateMessageAsync(string messageId, CancellationToken cancellationToken);
        Task AddMessageAsync(string messageId, string correlationId, string consumer, CancellationToken cancellationToken);
    }
}
