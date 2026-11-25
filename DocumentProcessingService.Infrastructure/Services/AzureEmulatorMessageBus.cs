using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.Configuration;
using DocumentProcessingService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace DocumentProcessingService.Infrastructure.Services
{

    public class AzureEmulatorMessageBus : IMessageBus
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly string _queueName;

        public AzureEmulatorMessageBus(ServiceBusClient serviceBusClient, IOptions<DocumentProcessingQueueOptions> options)
        {
            _serviceBusClient = serviceBusClient;
            _queueName = options.Value.QueueName;
        }

        public async Task PublishMessages(IEnumerable<ServiceBusMessage> messages, CancellationToken cancellationToken)
        {
           
            await using var queueSender = _serviceBusClient.CreateSender(_queueName);
            await queueSender.SendMessagesAsync(messages, cancellationToken);
        }

        public async Task PublishMessage(ServiceBusMessage message, CancellationToken cancellationToken)
        {
            await using var queueSender = _serviceBusClient.CreateSender(_queueName);
            await queueSender.SendMessageAsync(message, cancellationToken);
        }
    }
}
