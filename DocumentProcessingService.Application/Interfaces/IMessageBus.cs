using Azure.Messaging.ServiceBus;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IMessageBus
    {
        Task PublishMessage(ServiceBusMessage message, CancellationToken cancellationToken);
    }
}
