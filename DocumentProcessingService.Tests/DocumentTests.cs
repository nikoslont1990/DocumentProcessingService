using DocumentProcessingService.Domain.Entities;

namespace DocumentProcessingService.Tests
{
    public class DocumentTests
    {
        [Theory]
        [InlineData(DocumentStatus.Uploaded, DocumentStatus.Failed, true)]
        [InlineData(DocumentStatus.Uploaded, DocumentStatus.Processing, true)]
        [InlineData(DocumentStatus.Processing, DocumentStatus.Completed, true)]
        [InlineData(DocumentStatus.Processing, DocumentStatus.Failed, true)]
        [InlineData(DocumentStatus.Completed, DocumentStatus.Processing, false)]
        [InlineData(DocumentStatus.Failed, DocumentStatus.Completed, false)]
        [InlineData(DocumentStatus.Pending, DocumentStatus.Uploaded, true)]
        [InlineData(DocumentStatus.Pending, DocumentStatus.Failed, true)]
        public void TryTransition_WithVariousStatusTransitions_ReturnsExpectedValidity(DocumentStatus currentStatus,DocumentStatus newStatus, bool expectedStatus)
        {
            // Arrange
            var document = CreateDocumentInState(currentStatus);

            // Act
            document.TryTransition(currentStatus); // Set the initial status


            bool response = document.TryTransition(newStatus);

            // Assert
            Assert.Equal(expectedStatus, response);
        }

        private Document CreateDocumentInState(DocumentStatus targetStatus)
        {
            var doc = new Document();

            DocumentStatus[] transitions = targetStatus switch
            {
                DocumentStatus.Pending => Array.Empty<DocumentStatus>(),
                DocumentStatus.Uploaded => new[] { DocumentStatus.Uploaded },
                DocumentStatus.Processing => new[]
                {
            DocumentStatus.Uploaded,
            DocumentStatus.Processing
            },
                DocumentStatus.Completed => new[]
                {
            DocumentStatus.Uploaded,
            DocumentStatus.Processing,
            DocumentStatus.Completed
            },
                DocumentStatus.Failed => new[] { DocumentStatus.Failed },
                _ => throw new ArgumentOutOfRangeException(nameof(targetStatus))
            };

            foreach (var status in transitions)
            {
                if (!doc.TryTransition(status))
                    throw new InvalidOperationException($"Cannot transition to {status}");
            }

            return doc;
        }


        [Fact]
        public void TryTransition_WithStatusPendingTransitionToCompleted_ReturnsFalse()
        {
            // Arrange
            var document = new Document();

            // Act
            //document.TryTransition(DocumentStatus.Completed); // Set the initial status
            var status = document.Status; // Get the initial status

            bool response = document.TryTransition(DocumentStatus.Completed);

            // Assert
            Assert.False(response);
            Assert.Equal(status, document.Status);  
        }
    }
}
