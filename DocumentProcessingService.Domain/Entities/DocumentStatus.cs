using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using static System.Net.WebRequestMethods;

namespace DocumentProcessingService.Domain.Entities
{
    public enum DocumentStatus
    {
        [Display(Name = "Pending")]
        Pending = 0,

        [Display(Name = "Uploaded")]
        Uploaded = 1,

        [Display(Name = "Processing")]
        Processing = 2,

        [Display(Name = "Published")]
        Published = 3,

        [Display(Name = "Completed")]
        Completed = 4,

        [Display(Name = "Failed")]
        Failed = 5
    }
}