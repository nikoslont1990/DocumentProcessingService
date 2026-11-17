using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessingService.Infrastructure.Pesistence.Inbox
{
    public class InboxMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string MessageId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public Guid DocumentId { get; set; }
        public string CorrelationId { get; set; }
        public string Consumer { get; set; }
    }
}
