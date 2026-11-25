using Azure.Messaging.ServiceBus;

namespace DocumentProcessingService.Application.Interfaces
{
    public interface IMessageBus
    {
        Task PublishMessages(IEnumerable<ServiceBusMessage> message, CancellationToken cancellationToken);
        Task PublishMessage(ServiceBusMessage message, CancellationToken cancellationToken);

    }
}
