using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.Configuration;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly int _maxConcurrentCalls = 3;
        private readonly TimeSpan _messageLockDuration = TimeSpan.FromMinutes(1);


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
             await ConsumeFromQueueAsync(stoppingToken);
        }

        private async Task ConsumeFromQueueAsync(CancellationToken cancellationToken)
        {
            var processor = serviceBusClient.CreateProcessor(_queueName, new ServiceBusProcessorOptions()
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                AutoCompleteMessages = false,
                MaxConcurrentCalls = _maxConcurrentCalls,
                MaxAutoLockRenewalDuration = _messageLockDuration
            });

            processor.ProcessMessageAsync += ProcessMessageAsync;
            
            processor.ProcessErrorAsync += ProcessErrorAsync;

            logger.LogInformation("Starting Service Bus processor for queue {QueueName} with MaxConcurrentCalls={MaxConcurrentCalls} and MessageLockDuration={MessageLockDuration}"
                , _queueName, _maxConcurrentCalls, _messageLockDuration);
           
            try
            {
                await processor.StartProcessingAsync(cancellationToken);
                await Task.Delay(Timeout.Infinite, cancellationToken);

            }
            catch(OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Stopping processor due to cancellation request.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in processor");
            }
            finally
            {
                await processor.StopProcessingAsync();
                await processor.DisposeAsync();
                logger.LogInformation("Processor stopped and disposed");
            }
        }


        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            var ct = args.CancellationToken;
            var messageId = args.Message.MessageId;
            var correlationId = args.Message.CorrelationId;

            try
            {
                logger.LogDebug("Processing message. MessageId={MessageId}, CorrelationId={CorrelationId}",
                    messageId, correlationId);
                ProcessingOutcome outcome = await ProcessDocumentMessageAsync(args, ct);
                await HandleProcessingOutcomeAsync(args, outcome, messageId, correlationId);
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

        private async Task HandleProcessingOutcomeAsync(ProcessMessageEventArgs args, ProcessingOutcome outcome, string messageId, string correlationId)
        {
            var ct = args.CancellationToken;

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

        private async Task<ProcessingOutcome> ProcessDocumentMessageAsync(ProcessMessageEventArgs args, CancellationToken ct)
        {
            //string messageId, correlationId;
            var validationOutcome = ValidateMessage(args, out var documentMessage);
            if (validationOutcome.HasValue)
            {
                return validationOutcome.Value;
            }

            if (documentMessage == null)
            {
                logger.LogError("Document message is null after validation. MessageId={MessageId}",
                    args.Message.MessageId);
                return ProcessingOutcome.InvalidPayload;
            }

            return await ProcessDocumentAsync(args.Message.MessageId, 
                args.Message.CorrelationId, documentMessage, ct);
        }

        private async Task<ProcessingOutcome> ProcessDocumentAsync(string messageId, string correlationId, DocumentMessage? documentEvent, CancellationToken ct)
        {
            using var scope = serviceScopedFactory.CreateScope();
            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var inboxRepository = scope.ServiceProvider.GetRequiredService<IInboxRepository>();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();

            logger.LogInformation(
                "Received document {DocumentId}. MessageId {MessageId}, CorrelationId {CorrelationId}",
                documentEvent.DocumentId, messageId, correlationId);


            using var transaction = await dbContext.Database
                                       .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            try
            {
               
                //Step 1: Check if the message already exists in the inbox
                (bool isNew, bool alreadyProcessed) = await inboxRepository.AddMessageIfNotExistsAsync(messageId, correlationId, documentEvent.DocumentId, nameof(DocumentProcessingWorker), ct);

                if (!isNew)
                {
                    logger.LogWarning(
                    "Duplicate message detected. MessageId={MessageId}, AlreadyProcessed={AlreadyProcessed}",
                    messageId, alreadyProcessed);
                    return ProcessingOutcome.Duplicate;
                }

                //Step 2: Mark the document as completed
                var result = await documentRepository.MarkDocumentAsCompleted(documentEvent.DocumentId, ct);

                if (result == DocumentCompletionResult.NotFound)
                {
                    logger.LogWarning(
                    "Document not found (retryable). MessageId={MessageId}, DocumentId={DocumentId}",
                    messageId, documentEvent.DocumentId);

                    return ProcessingOutcome.DocumentNotFoundRetryable;
                }

                //Step 3: Save changes 
                await documentRepository.SaveAsync(ct);
                await inboxRepository.MarkAsProcessedAsync(messageId, ct);
                await documentRepository.SaveAsync(ct);

                // SUCCESS - Commit everything  
                await transaction.CommitAsync(ct);

                logger.LogInformation(
                "Successfully processed message {MessageId} for document {DocumentId}",
                messageId, documentEvent.DocumentId);

                return ProcessingOutcome.Completed;
            }
            catch (DbUpdateException ex) when (IsUniqueInboxMessageIdViolation(ex))
            {
                await transaction.RollbackAsync(ct);

                logger.LogWarning(ex,
                    "Unique constraint violation during save. MessageId={MessageId}. Another worker processed this.",
                    messageId);
                return ProcessingOutcome.Duplicate;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await transaction.RollbackAsync(ct);
                logger.LogInformation("Message processing cancelled. MessageId={MessageId}", messageId);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);

                logger.LogError(ex,
                    "Unexpected error processing document. MessageId={MessageId}, DocumentId={DocumentId}",
                    messageId, documentEvent.DocumentId);

                throw;
            }
        }


        private ProcessingOutcome? ValidateMessage(ProcessMessageEventArgs args, 
            out DocumentMessage? documentMessage)
        {
            documentMessage = null;
            var messageId = args.Message.MessageId;

            if (string.IsNullOrWhiteSpace(messageId))
                return ProcessingOutcome.MissingMessageId;

            try
            {
                documentMessage = JsonConvert.DeserializeObject<DocumentMessage>(args.Message.Body.ToString());
                if (documentMessage?.DocumentId == Guid.Empty)
                {
                    logger.LogError("Document has empty DocumentId. MessageId={MessageId}", messageId);
                    return ProcessingOutcome.InvalidPayload;
                }

                return null;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize document. MessageId={MessageId}", messageId);
                return ProcessingOutcome.InvalidPayload;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during message validation. MessageId={MessageId}", messageId);
                return ProcessingOutcome.InvalidPayload;
            }
        }
    }
}
    
