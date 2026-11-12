using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.Interfaces;

namespace DocumentProcessingService.Infrastructure.Services
{

    public class AzureEmulatorMessageBus : IMessageBus
    {
        private readonly ServiceBusClient _serviceBusClient;

        public AzureEmulatorMessageBus(ServiceBusClient serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        public async Task PublishMessage(ServiceBusMessage message, CancellationToken cancellationToken)
        {
           
            await using var queueSender = _serviceBusClient.CreateSender("queue.1");
            await queueSender.SendMessageAsync(message, cancellationToken);
        }
    }
}
