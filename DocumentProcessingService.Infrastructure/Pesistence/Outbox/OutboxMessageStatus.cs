using System.ComponentModel.DataAnnotations;

namespace DocumentProcessingService.Infrastructure.Pesistence.Outbox
{
    public enum OutboxMessageStatus
    {
        [Display(Name = "Pending")]
        Pending = 0,

        [Display(Name = "Published")]
        Published = 1,

        [Display(Name = "Failed")]
        Failed = 2
    }
}