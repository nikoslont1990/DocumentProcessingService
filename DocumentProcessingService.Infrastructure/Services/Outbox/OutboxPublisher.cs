using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace DocumentProcessingService.Infrastructure.Services.Outbox
{
    public class OutboxPublisher : IOutboxPublisher
    {
        private readonly IOutboxRepository _outboxRepository;
        private readonly ILogger<OutboxPublisher> _logger;
        private readonly IMessageBus _messageBus;
        private readonly IConfiguration _configuration;

        public OutboxPublisher(IOutboxRepository outboxRepository, IMessageBus messageBus,
            ILogger<OutboxPublisher> logger, IConfiguration configuration)
        {
            _outboxRepository = outboxRepository;
            _messageBus = messageBus;
            _logger = logger;
            _configuration = configuration;
        }
        public async Task<PublishCycleResult> PublishPendingMessagesAsync(CancellationToken cancellationToken)
        {
            var maxRetries = _configuration.GetValue<int?>("Outbox:MaxRetries") ?? 5;
            var records = await _outboxRepository.GetPendingMessagesAsync(cancellationToken);
            var fetched = records.Count;
            var published = 0;
            var failed = 0;


            string messageBody = string.Empty;
            foreach (OutboxMessageDTO rec in records)
            {
                try
                {
                    //string messageBody = JsonSerializer.Serialize(rec.Content);
                    messageBody = rec.Content;
                    var message = new ServiceBusMessage(messageBody)
                    {
                        MessageId = rec.Id.ToString(),
                        Subject = rec.Type,
                        ContentType = "application/json"
                    };
                    await _messageBus.PublishMessage(message, cancellationToken);
                    await _outboxRepository.MarkedAsPulished(rec.Id, cancellationToken);
                    published++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish message with Id: {MessageId}", rec.Id);
                    await _outboxRepository.MarkedAsFailed(rec.Id, ex.Message, maxRetries, cancellationToken);
                    failed++;
                }
            }
            await _outboxRepository.SaveAsync(cancellationToken);
            _logger.LogInformation("All pending messages published successfully");
            _logger.LogInformation("Message published successfully");
            return new PublishCycleResult(fetched, published, failed);

        }

        private async Task RetryLogic(Guid outboxId, string error, int maxRetries)
        {
            
        }

        static void Retry()
        {
            // Retry logic here
        }
    }
}