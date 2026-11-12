using DocumentProcessingService.Domain.Entities;
using Grpc.Core;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessingService.Infrastructure.Pesistence.Outbox
{
    public class OutboxMessage
    {
        private Dictionary<OutboxMessageStatus, OutboxMessageStatus[]> _allowedTransitions;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string Type { get; set; }

        //Where the Json serialization will be saved
        public string Content { get; set; }

        public DateTime? PublishedAt { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public DateTime NextRetryAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

        public OutboxMessage()
        {
            _allowedTransitions = ConfigureTransitions();
        }
        public bool TryOutboxStatusTransition(OutboxMessageStatus newStatus)
        {
            if (!_allowedTransitions.TryGetValue(Status, out var allowedTransitions))
            {
                return false;
            }

            if (!allowedTransitions.Contains(newStatus))
            {
                return false;
            }

            Status = newStatus;
            return true;
        }

        private Dictionary<OutboxMessageStatus, OutboxMessageStatus[]> ConfigureTransitions()
        {
            return new Dictionary<OutboxMessageStatus, OutboxMessageStatus[]>
            {
                { OutboxMessageStatus.Pending, new[] { OutboxMessageStatus.Published, OutboxMessageStatus.Failed } },
                { OutboxMessageStatus.Published, new[] { OutboxMessageStatus.Failed } },
                { OutboxMessageStatus.Failed, new[] { OutboxMessageStatus.Pending } },
            };
        }

    }
}
