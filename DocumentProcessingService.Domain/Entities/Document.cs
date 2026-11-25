
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessingService.Domain.Entities
{
    public class Document
    {
        private Dictionary<DocumentStatus, DocumentStatus[]> _allowedTransitions;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string FileName { get; set; }

        public string ContentType { get; set; } = string.Empty;

        public DocumentStatus Status { get;private set; } = DocumentStatus.Pending;

        public DateTime? CreatedAt { get; set; }

        public string BlobName { get; set; } = string.Empty;


        public Document()
        {
            _allowedTransitions = ConfigureTransitions();
        }
        public bool TryTransition(DocumentStatus newStatus)
        {
            if (!_allowedTransitions.TryGetValue(Status, out var allowedTransitions))
            {
                //Console.WriteLine($"Επιτυχής μετάβαση: {CurrentState} -> {nextState} μέσω του {trigger}");
                //CurrentState = nextState;
                return false;
            }

            if (!allowedTransitions.Contains(newStatus))
            {
                return false;
            }

            Status = newStatus;
            return true;
        }

        private Dictionary<DocumentStatus, DocumentStatus[]> ConfigureTransitions()
        {
            return new Dictionary<DocumentStatus, DocumentStatus[]>
            {
                { DocumentStatus.Pending, new[] { DocumentStatus.Uploaded, DocumentStatus.Failed } },
                { DocumentStatus.Uploaded, new[] { DocumentStatus.Completed, DocumentStatus.Failed } },
                //{ DocumentStatus.Processing, new[] { DocumentStatus.Completed, DocumentStatus.Failed } },
            };
        }
    }
}
