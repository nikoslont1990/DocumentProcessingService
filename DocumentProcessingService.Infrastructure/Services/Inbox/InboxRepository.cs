using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Pesistence.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentProcessingService.Infrastructure.Services.Inbox
{
    public class InboxRepository : IInboxRepository
    {
        private readonly DocumentDbContext _documentDbContext;
        private readonly ILogger<InboxRepository> _logger;

        public InboxRepository(DocumentDbContext documentDbContext, ILogger<InboxRepository> logger)
        {
            _documentDbContext = documentDbContext;
            _logger = logger;
        }

        public async Task<bool> AlreadyExists(string messageId,CancellationToken cancellationToken)
        {
            var messageExists = await _documentDbContext.InboxMessages
                                                         .AsNoTracking()
                                                         .AnyAsync(it => it.MessageId == messageId && it.ProcessedAt!= null, cancellationToken);
            return messageExists;
        }

        public async Task AddMessageAsync(string messageId, string correlationId, Guid documentId, string consumer, CancellationToken cancellationToken)
        {
            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                ProcessedAt = DateTime.UtcNow,
                DocumentId = documentId,
                CorrelationId = correlationId,
                Consumer = consumer
            };

            await _documentDbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);
        }

        public async Task<(bool isNew, bool alreadyProcessed)> AddMessageIfNotExistsAsync(
            string messageId,
            string correlationId,
            Guid documentId,
            string consumer,
            CancellationToken cancellationToken)
        {
            try
            {
                var existing = await _documentDbContext.InboxMessages.FirstOrDefaultAsync(it => it.MessageId == messageId, cancellationToken);
                if (existing != null)
                {
                    bool alreadyProcessed = existing.ProcessedAt != null;

                    _logger.LogWarning(
                        "Duplicate message detected. MessageId={MessageId}, AlreadyProcessed={AlreadyProcessed}",
                        messageId, alreadyProcessed);

                    return (false, alreadyProcessed);
                }

                var inboxMessage = new InboxMessage
                {
                    MessageId = messageId,
                    ProcessedAt = DateTime.UtcNow,
                    DocumentId = documentId,
                    CorrelationId = correlationId,
                    Consumer = consumer
                };

                await _documentDbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);
                _logger.LogDebug("New inbox message staged for insert. MessageId={MessageId}", messageId);

                return (true, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding inbox message. MessageId={MessageId}", messageId);
                throw;
            }
        }

        /// <summary>
        /// Marks an inbox message as processed.
        /// </summary>
        public async Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken)
        {
            var message = await _documentDbContext.InboxMessages
                .FirstOrDefaultAsync(x => x.MessageId == messageId, cancellationToken);

            if (message != null)
            {
                message.ProcessedAt = DateTime.UtcNow;
                _logger.LogDebug("Inbox message marked as processed. MessageId={MessageId}", messageId);
            }
        }
    }
}
