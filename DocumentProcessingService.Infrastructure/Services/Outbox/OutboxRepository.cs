using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Pesistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessingService.Infrastructure.Services.Outbox
{
    public class OutboxRepository : IOutboxRepository
    {
        private readonly DocumentDbContext _documentDbContext;

        public OutboxRepository(DocumentDbContext documentDbContext)
        {
            _documentDbContext = documentDbContext;
        }


        public async Task AddAsync(string type, string content, CancellationToken cancellationToken)
        {

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = type,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            await _documentDbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        }

        public async Task<IReadOnlyList<OutboxMessageDTO>> GetPendingMessagesAsync(CancellationToken cancellationToken)
        {
            List<OutboxMessageDTO> pendingMessages = await _documentDbContext.OutboxMessages
                .Where(it => it.PublishedAt == null &&
                       it.Status == OutboxMessageStatus.Pending &&
                       (it.NextRetryAt == null ||
                       it.NextRetryAt <= DateTime.UtcNow))
                .Select(it => new OutboxMessageDTO
                       {
                           Id = it.Id,
                           Type = it.Type,
                           Content = it.Content,
                           CreatedAt = it.CreatedAt
                       })
                .OrderBy(it => it.CreatedAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return pendingMessages;
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            await _documentDbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkAsPulished(Guid id, CancellationToken cancellationToken)
        {
            var record = await _documentDbContext.OutboxMessages.FirstOrDefaultAsync(it => it.Id == id, cancellationToken);

            if (record == null)
            {
                return;
            }

            record.PublishedAt = DateTime.UtcNow;
            record.TryOutboxStatusTransition(OutboxMessageStatus.Published);
        }

        public async Task MarkAsFailed(Guid id, string error, int maxRetries, CancellationToken cancellationToken)
        {
            var record = await _documentDbContext.OutboxMessages
                                                 .FirstOrDefaultAsync(it => it.Id == id, cancellationToken);

            if (record == null)
            {
                return;
            }


            record.RetryCount++;
            record.NextRetryAt = DateTime.UtcNow.AddSeconds(20); // Example retry delay
            if(record.RetryCount >= maxRetries)
            {
                record.TryOutboxStatusTransition(OutboxMessageStatus.Failed); // give up
            }
            else
            {
                record.TryOutboxStatusTransition(OutboxMessageStatus.Pending); //retry later
            }
        }
    }
}
