using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IOutboxRepository
    {
        Task AddAsync(string type,string content, CancellationToken cancellationToken);

        Task<IReadOnlyList<OutboxMessageDTO>> GetPendingMessagesAsync(CancellationToken cancellationToken);
        Task MarkAsPulished(Guid id, CancellationToken cancellationToken);

        Task MarkAsFailed(Guid id, string error, int maxRetries, CancellationToken cancellationToken);

        Task SaveAsync(CancellationToken cancellationToken);
    }
}
