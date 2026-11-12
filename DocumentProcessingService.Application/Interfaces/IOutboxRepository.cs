using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IOutboxRepository
    {
        Task AddAsync(string type,string content, CancellationToken cancellationToken);

        Task<IReadOnlyList<OutboxMessageDTO>> GetPendingMessagesAsync(CancellationToken cancellationToken);
        //Task ChangeStatus(Guid id,DocumentStatus status, CancellationToken cancellationToken);
        Task MarkedAsPulished(Guid id, CancellationToken cancellationToken);

        Task MarkedAsFailed(Guid id, string error, int maxRetries, CancellationToken cancellationToken);

        Task SaveAsync(CancellationToken cancellationToken);
    }
}
