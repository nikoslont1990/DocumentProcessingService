using Azure.Messaging.ServiceBus;

namespace DocumentProcessingService.Infrastructure.Services.Outbox
{
    public class BatchCreationResult
    {
        public List<ServiceBusMessage> batchMessages { get; set; }
        public  List<Guid> messageIds { get; set; }
    }
}