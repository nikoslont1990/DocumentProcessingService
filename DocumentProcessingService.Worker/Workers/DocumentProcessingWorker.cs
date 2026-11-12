using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;
using Newtonsoft.Json;
using DocumentMessage = DocumentProcessingService.Worker.Models.DocumentMessage;

namespace DocumentProcessingService.Worker.Workers
{
    public class DocumentProcessingWorker(ServiceBusClient serviceBusClient, IServiceScopeFactory serviceScopedFactory,
        IConfiguration configuration, ILogger<DocumentProcessingWorker> logger) 
        : BackgroundService, IDisposable
    {
        private readonly string queueName = configuration["ServiceBus:QueueName"];
        private ServiceBusProcessor? _processor;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
             await ConsumeFromQueueAsync(stoppingToken);
        }

        private async Task ConsumeFromQueueAsync(CancellationToken cancellationToken)
        {
            await using var processor = serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions()
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

            processor.ProcessMessageAsync += args => ProcessMessageAsync(args, "queue");
            
            processor.ProcessErrorAsync += ProcessErrorAsync;

            logger.LogInformation("Starting Service Bus processor for queue {QueueName}", queueName);
            await processor.StartProcessingAsync();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);

            }
            catch(OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {

            }
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args,string name)
        {

            try
            {
                var documentEvent = JsonConvert.DeserializeObject<DocumentMessage>(args.Message.Body.ToString());


                if (documentEvent == null || documentEvent.DocumentId == Guid.Empty)
                {
                    await args.DeadLetterMessageAsync(
                    args.Message,
                    "InvalidPayload",
                    "Payload could not be deserialized or DocumentId is empty.",
                    args.CancellationToken);
                    return;
                }


                using var scope = serviceScopedFactory.CreateScope();

                var repository = scope.ServiceProvider
                    .GetRequiredService<IDocumentRepository>();
                var response = documentEvent;
                logger.LogInformation($"Received document with Id : {response?.DocumentId}");
                await repository.MarkDocumentAsCompleted(documentEvent.DocumentId, args.CancellationToken);
                await repository.SaveAsync(args.CancellationToken);
                await Task.Delay(2000, args.CancellationToken);

                // Complete the message receive operation
                await args.CompleteMessageAsync(args.Message);
                logger.LogInformation("Processed message {MessageId} for DocumentId {DocumentId}",
                args.Message.MessageId, documentEvent.DocumentId);
            }
            catch (JsonException ex)
            {
                logger.LogInformation($"Error processing message: {ex.Message}");
                await args.DeadLetterMessageAsync(args.Message);
            }
            catch(ServiceBusException ex) when (ex.IsTransient)
            {
                logger.LogInformation($"Service Bus error processing message: {ex.Message}");
                await args.AbandonMessageAsync(args.Message);
            }
            catch(TimeoutException ex)
            {
                logger.LogInformation($"Timeout error processing message: {ex.Message}");
                await args.AbandonMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Error processing message: {ex.Message}");
                await args.AbandonMessageAsync(args.Message);
            }
        }

        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception,
            "Processor error. Entity={EntityPath}, Source={ErrorSource}, Namespace={FullyQualifiedNamespace}",
            args.EntityPath, args.ErrorSource, args.FullyQualifiedNamespace);

            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_processor is not null)
            {
                await _processor.StartProcessingAsync(cancellationToken);
                await _processor.DisposeAsync();
                _processor = null;
            }
            await base.StopAsync(cancellationToken);
        }
    }
}
    
