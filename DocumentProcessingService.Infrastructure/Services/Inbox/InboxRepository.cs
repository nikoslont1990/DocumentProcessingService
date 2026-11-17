using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Pesistence.Inbox;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessingService.Infrastructure.Services.Inbox
{
    public class InboxRepository : IInboxRepository
    {
        private readonly DocumentDbContext _documentDbContext;

        public InboxRepository(DocumentDbContext documentDbContext)
        {
            _documentDbContext = documentDbContext;
        }

        public async Task<bool> IsDuplicateMessageAsync(string messageId,CancellationToken cancellationToken)
        {
            var messageExists = await _documentDbContext.InboxMessages.AnyAsync(it => it.MessageId == messageId, cancellationToken);
            return messageExists;
        }

        public async Task AddMessageAsync(string messageId, string correlationId, string consumer, CancellationToken cancellationToken)
        {
            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                ProcessedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                Consumer = consumer
            };

            await _documentDbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);
        }
    }
}
