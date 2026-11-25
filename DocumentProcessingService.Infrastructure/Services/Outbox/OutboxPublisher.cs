using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;


namespace DocumentProcessingService.Infrastructure.Services.Outbox
{
    public class OutboxPublisher : IOutboxPublisher
    {
        private readonly IOutboxRepository _outboxRepository;
        private readonly ILogger<OutboxPublisher> _logger;
        private readonly IMessageBus _messageBus;
        private readonly IConfiguration _configuration;

        private const int _batchSize = 10;

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
            var maxRetries = int.TryParse(_configuration["Outbox:MaxRetries"], out var value) ? value : 5;
            var records = await _outboxRepository.GetPendingMessagesAsync(cancellationToken);

            if (records.Count == 0)
            {
                _logger.LogInformation("No pending messages to publish");
                return new PublishCycleResult(0, 0, 0);
            }

            var sw = Stopwatch.StartNew();
            var result = await PublishInBatchesAsync(records, maxRetries, cancellationToken);
            sw.Stop();

            _logger.LogInformation(
                "Publishing cycle completed. Fetched={Fetched}, Published={Published}, Failed={Failed}, Duration={Duration}ms",
                records.Count, result.Published, result.Failed, sw.ElapsedMilliseconds);

            await _outboxRepository.SaveAsync(cancellationToken);

            return result;

        }

        /// <summary>
        /// Publish messages in batches (RECOMMENDED - Optimized)
        /// </summary>
        private async Task<PublishCycleResult> PublishInBatchesAsync(
            IReadOnlyList<OutboxMessageDTO> records,
            int maxRetries,
            CancellationToken cancellationToken)
        {
            var published = 0;
            var failed = 0;
            int batchSize = _batchSize;

            for (int i = 0; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize).ToList();
                var result = await PublishBatchAsync(batch, maxRetries, cancellationToken);
                published += result.Published;
                failed += result.Failed;
            }

            return new PublishCycleResult(records.Count, published, failed);
        }


        private async Task<BatchPublishResult> PublishBatchAsync(
            List<OutboxMessageDTO> batch,
            int maxRetries,
            CancellationToken cancellationToken
            )
        {
            BatchCreationResult batchObj = null;
            var published = 0;
            var failed = 0;

            try
            {
                batchObj = CreateBatch(batch);
                // Send entire batch in one network call

                var batchSw = Stopwatch.StartNew();
                _logger.LogInformation("Publishing batch of {Count} messages to queue '{QueueName}'",
                    batchObj.batchMessages.Count,
                    _configuration["Workers:DocumentProcessing:QueueName"]);

                await _messageBus.PublishMessages(batchObj.batchMessages, cancellationToken);
                batchSw.Stop();

                _logger.LogDebug(
                "Batch published successfully. Count={Count}, Duration={Duration}ms, AvgPerMessage={AvgTime}ms",
                batchObj.batchMessages.Count, batchSw.ElapsedMilliseconds,
                batchSw.ElapsedMilliseconds / (double)batchObj.batchMessages.Count);
                // Mark all in batch as published

                foreach (var id in batchObj.messageIds)
                {
                    await _outboxRepository.MarkAsPulished(id, cancellationToken);
                }
                published += batchObj.batchMessages.Count;
            }
            catch (OperationCanceledException)
            {
                throw;  // Re-throw immediately
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish batch of {Count} messages", batchObj.batchMessages.Count);

                // Mark all in batch as failed
                foreach (var id in batchObj.messageIds)
                {
                    await _outboxRepository.MarkAsFailed(id, ex.Message, maxRetries, cancellationToken);
                }
                failed += batchObj.batchMessages.Count;
            }
            return new BatchPublishResult(published, failed);
        }

        private static BatchCreationResult CreateBatch(List<OutboxMessageDTO> batch)
        {
            var batchMessages = new List<ServiceBusMessage>();
            var messageIds = new List<Guid>();
            foreach (OutboxMessageDTO rec in batch)
            {
                var message = new ServiceBusMessage(rec.Content)
                {
                    MessageId = rec.Id.ToString(),
                    CorrelationId = rec.Id.ToString(),
                    Subject = rec.Type,
                    ContentType = "application/json"
                };
                batchMessages.Add(message);
                messageIds.Add(rec.Id);
            }

            return new BatchCreationResult
            {
                batchMessages = batchMessages,
                messageIds = messageIds
            };
        }

        public async Task<PublishCycleResult> PublishPendingMessagesOneByOne(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Using ONE-BY-ONE publishing (slow). Switch to batch mode for production!");

            var maxRetries = int.TryParse(_configuration["Outbox:MaxRetries"], out var value) ? value : 5;
            var records = await _outboxRepository.GetPendingMessagesAsync(cancellationToken);

            if (records.Count == 0)
            {
                _logger.LogInformation("No pending messages to publish");
                return new PublishCycleResult(0, 0, 0);
            }

            var published = 0;
            var failed = 0;
            var sw = Stopwatch.StartNew();

            foreach (var rec in records)
            {
                try
                {

                    var message = new ServiceBusMessage(rec.Content)
                    {
                        MessageId = rec.Id.ToString(),
                        CorrelationId = rec.Id.ToString(),
                        Subject = rec.Type,
                        ContentType = "application/json"
                    };


                    await _messageBus.PublishMessage(message, cancellationToken);

                    await _outboxRepository.MarkAsPulished(rec.Id, cancellationToken);
                    published++;
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish message {MessageId}", rec.Id);
                    await _outboxRepository.MarkAsFailed(rec.Id, ex.Message, maxRetries, cancellationToken);
                    failed++;
                }
            }

            sw.Stop();

            _logger.LogInformation(
                "ONE-BY-ONE publishing completed. Total={Total}, Published={Published}, Failed={Failed}, Duration={Duration}ms",
                records.Count, published, failed, sw.ElapsedMilliseconds);

            await _outboxRepository.SaveAsync(cancellationToken);

            return new PublishCycleResult(records.Count, published, failed);
        }


    }
}