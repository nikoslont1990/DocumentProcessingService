using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Pesistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit.Sdk;

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
            var pendingMessages = await _documentDbContext.OutboxMessages
                .Where(it => it.PublishedAt == null)
                .OrderBy(it => it.CreatedAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

           return pendingMessages.Select(it => new OutboxMessageDTO
           {
               Id = it.Id,
               Type = it.Type,
               Content = it.Content,
               CreatedAt = it.CreatedAt
           }).ToList();
        }

        //public async Task ChangeStatus(Guid id, DocumentStatus status, CancellationToken cancellationToken)
        //{
        //    var record = await _documentDbContext.OutboxMessages.FirstOrDefaultAsync(it => it.Id == id);

        //    if (record == null)
        //    {
        //        return;
        //    }

        //        record.PublishedAt = DateTime.Now;
        //        var documentMessage = JsonConvert.DeserializeObject<DocumentMessage>(record.Content);
        //        var recDocument = await _documentDbContext.Documents
        //                                                    .FirstOrDefaultAsync(it => it.Id == documentMessage.DocumentId);
        //        recDocument.TryTransition(status);

        //}

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            await _documentDbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkedAsPulished(Guid id, CancellationToken cancellationToken)
        {
            var record = await _documentDbContext.OutboxMessages.FirstOrDefaultAsync(it => it.Id == id);

            if (record == null)
            {
                return;
            }

            record.PublishedAt = DateTime.UtcNow;
            record.TryOutboxStatusTransition(OutboxMessageStatus.Published);
        }

        public async Task MarkedAsFailed(Guid id, string error, int maxRetries, CancellationToken cancellationToken)
        {
            var record = await _documentDbContext.OutboxMessages.FirstOrDefaultAsync(it => it.Id == id);

            if (record == null)
            {
                return;
            }


            record.RetryCount++;
            //record.LastError = errorMessage;
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
