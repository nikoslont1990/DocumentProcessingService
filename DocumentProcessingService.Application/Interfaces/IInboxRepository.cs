using Microsoft.Azure.Amqp.Framing;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IInboxRepository
    {
        Task<bool> AlreadyExists(string messageId, CancellationToken cancellationToken);
        Task AddMessageAsync(string messageId, string correlationId, Guid documentId, string consumer, CancellationToken cancellationToken);

        Task<(bool isNew, bool alreadyProcessed)> AddMessageIfNotExistsAsync(string messageId, string correlationId, Guid documentId, string consumer, CancellationToken cancellationToken);

        Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken);
    }
}
