using System.ComponentModel.DataAnnotations;

namespace DocumentProcessingService.Application.DTOs
{
    public enum DocumentCompletionResult
    {
        [Display(Name = "CompletedNow")]
        CompletedNow = 0,

        [Display(Name = "AlreadyCompleted")]
        AlreadyCompleted = 1,

        [Display(Name = "NotFound")]
        NotFound = 2
    }
}
