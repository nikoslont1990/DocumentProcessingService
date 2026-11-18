using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using DocumentMessage = DocumentProcessingService.Worker.Models.DocumentMessage;

namespace DocumentProcessingService.Worker.Workers
{
    public class DocumentProcessingWorker(ServiceBusClient serviceBusClient, IServiceScopeFactory serviceScopedFactory,
        IOptions<DocumentProcessingQueueOptions> queueOptions,
        IConfiguration configuration, ILogger<DocumentProcessingWorker> logger) 
        : BackgroundService, IDisposable
    {
        private readonly string _queueName = queueOptions.Value.QueueName;
        //private ServiceBusProcessor? _processor;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
             await ConsumeFromQueueAsync(stoppingToken);
        }

        private async Task ConsumeFromQueueAsync(CancellationToken cancellationToken)
        {
            await using var processor = serviceBusClient.CreateProcessor(_queueName, new ServiceBusProcessorOptions()
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });

            processor.ProcessMessageAsync += ProcessMessageAsync;
            
            processor.ProcessErrorAsync += ProcessErrorAsync;

            logger.LogInformation("Starting Service Bus processor for queue {QueueName}", _queueName);
            await processor.StartProcessingAsync();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);

            }
            catch(OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {

            }
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            var ct = args.CancellationToken;
            var correlationId = args.Message.CorrelationId;
            var messageId = args.Message.MessageId;

            try
            {
                ProcessingOutcome outcome = await ProcessDocumentMessageAsync(args, ct);

                switch (outcome)
                {
                    case ProcessingOutcome.Completed:
                    case ProcessingOutcome.Duplicate:
                        await args.CompleteMessageAsync(args.Message, ct);
                        break;

                    case ProcessingOutcome.MissingMessageId:
                        await args.DeadLetterMessageAsync(
                            args.Message, "MissingMessageId", "MessageId is required.", ct);
                        break;

                    case ProcessingOutcome.InvalidPayload:
                        await args.DeadLetterMessageAsync(
                            args.Message, "InvalidPayload", "Payload could not be deserialized or DocumentId is empty.", ct);
                        break;

                    case ProcessingOutcome.DocumentNotFoundRetryable:
                        logger.LogWarning(
                            "Document not found yet. Abandoning for retry. MessageId {MessageId}, CorrelationId {CorrelationId}",
                            messageId, correlationId);

                        await args.AbandonMessageAsync(args.Message, null, ct);
                        break;
                }
            }
            catch (ServiceBusException ex) when (ex.IsTransient)
            {
                logger.LogWarning(ex,
                    "Transient Service Bus error. MessageId {MessageId}, CorrelationId {CorrelationId}",
                    messageId, correlationId);

                await args.AbandonMessageAsync(args.Message, null, ct);
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex,
                    "Timeout while processing message. MessageId {MessageId}, CorrelationId {CorrelationId}",
                    messageId, correlationId);

                await args.AbandonMessageAsync(args.Message, null, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unhandled processing error. MessageId {MessageId}, CorrelationId {CorrelationId}",
                    messageId, correlationId);

                await args.AbandonMessageAsync(args.Message, null, ct);
            }
        }

        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception,
            "Processor error. Entity={EntityPath}, Source={ErrorSource}, Namespace={FullyQualifiedNamespace}",
            args.EntityPath, args.ErrorSource, args.FullyQualifiedNamespace);

            return Task.CompletedTask;
        }

        private static bool IsUniqueInboxMessageIdViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sqlEx
                   && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
        }

        public async Task<ProcessingOutcome> ProcessDocumentMessageAsync(ProcessMessageEventArgs args, CancellationToken ct)
        {
            string messageId, correlationId;
            DocumentMessage? documentEvent;
            var validationOutcome = ValidateMessage(args);
            if (validationOutcome.HasValue)
            {
                return validationOutcome.Value;
            }

            // Step 2: Extract the document from JSON
            var message = JsonConvert.DeserializeObject<DocumentMessage>(args.Message.Body.ToString());


            //Step 3: Process the document
            return await ProcessDocumentAsync(args.Message.MessageId, args.Message.CorrelationId, message, ct);
        }

        private async Task<ProcessingOutcome> ProcessDocumentAsync(string messageId, string correlationId, DocumentMessage? documentEvent, CancellationToken ct)
        {
            using var scope = serviceScopedFactory.CreateScope();
            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();

            logger.LogInformation(
                "Received document {DocumentId}. MessageId {MessageId}, CorrelationId {CorrelationId}",
                documentEvent.DocumentId, messageId, correlationId);

            if (await inboxRepository.AlreadyExists(messageId, ct))
                return ProcessingOutcome.Duplicate;

            var result = await documentRepository.MarkDocumentAsCompleted(documentEvent.DocumentId, ct);
            if (result == DocumentCompletionResult.NotFound)
                return ProcessingOutcome.DocumentNotFoundRetryable;

            await inboxRepository.AddMessageAsync(messageId, correlationId, nameof(DocumentProcessingWorker), ct);

            try
            {
                await documentRepository.SaveAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueInboxMessageIdViolation(ex))
            {
                return ProcessingOutcome.Duplicate;
            }

            logger.LogInformation(
                "Processed message {MessageId} for document {DocumentId}",
                messageId, documentEvent.DocumentId);

            return ProcessingOutcome.Completed;
        }

        private ProcessingOutcome? ValidateMessage(ProcessMessageEventArgs args)
        {
            var messageId = args.Message.MessageId;

            if (string.IsNullOrWhiteSpace(messageId))
                return ProcessingOutcome.MissingMessageId;

            DocumentMessage? documentEvent = null;
            try
            {
                documentEvent = JsonConvert.DeserializeObject<DocumentMessage>(args.Message.Body.ToString());
            }
            catch (JsonException)
            {
                return ProcessingOutcome.InvalidPayload;
            }

            if (documentEvent?.DocumentId == Guid.Empty)
                return ProcessingOutcome.InvalidPayload;
          
            return null;
        }
    }
}
    
