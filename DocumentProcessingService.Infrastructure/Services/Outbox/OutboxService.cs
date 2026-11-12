using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Pesistence.Outbox;

namespace DocumentProcessingService.Infrastructure.Services.Outbox
{
    public class OutboxService : IOutboxService
    {
        private readonly IOutboxRepository _outboxRepository;
        private readonly DocumentDbContext _documentDbContext;

        public OutboxService(IOutboxRepository outboxRepository, DocumentDbContext documentDbContext)
        {
            _documentDbContext = documentDbContext;
            _outboxRepository = outboxRepository;
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

            await _outboxRepository.AddAsync(type, content, cancellationToken);
        }
    }
}
