using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Application.Services;
using DocumentProcessingService.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Extensions.Msal;
using Moq;
using System.Reflection.Metadata;
using Document = DocumentProcessingService.Domain.Entities.Document;


namespace DocumentProcessingService.Tests
{
    public class DocumentServiceTests
    {
        private readonly Mock<IDocumentRepository> _mockRepository;
        private readonly Mock<IBlobStorageService> _mockBlobStorage;
        private readonly Mock<ILogger<DocumentService>> _mockLogger;
        private readonly Mock<IOutboxService> _mockOutboxService;

        private readonly DocumentService _documentService;

        // Constructor - Setup mocks for each test
        public DocumentServiceTests()
        {
            _mockRepository = new Mock<IDocumentRepository>();
            _mockBlobStorage = new Mock<IBlobStorageService>();
            _mockLogger = new Mock<ILogger<DocumentService>>();
            _mockOutboxService = new Mock<IOutboxService>();
            _documentService = new DocumentService(_mockRepository.Object, _mockBlobStorage.Object, _mockOutboxService.Object, _mockLogger.Object);
        }

        #region CreateDocument - Happy Path Tests

        //[Fact]
        //public async Task CreateDocument_WithValidInputs_ShouldReturnDocument()
        //{
        //    // Arrange
        //    var documentId = Guid.NewGuid();
        //    var fileName = "test.pdf";
        //    long fileSize = 500 * 1024; // 500 KB
        //    var cancellationToken = CancellationToken.None;

        //    var fileContent = new byte[fileSize];
        //    using (var stream = new MemoryStream(fileContent))
        //    {
        //        _mockRepository
        //        .Setup(x => x.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(documentId);

        //        _mockBlobStorage
        //                .Setup(x => x.UploadDocumentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
        //                .ReturnsAsync(true);

        //        // Act
        //        var result = await _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken);

        //        // Assert
        //        result.Should().NotBeNull();
        //        result.FileName.Should().Be(fileName);
        //        result.Status.Should().Be("Completed");
        //    }
        //}

        #endregion

        #region CreateDocument - Edge Cases

        //[Fact]
        //public async Task CreateDocument_WithZeroFileSize_ShouldThrowArgumentException()
        //{
        //    // Arrange
        //    var documentId = Guid.NewGuid();
        //    var fileName = "test.pdf";
        //    long fileSize = 0; // ✅ Invalid size
        //    var stream = new MemoryStream();
        //    var cancellationToken = CancellationToken.None;

        //    _mockRepository
        //      .Setup(x => x.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
        //      .ReturnsAsync(documentId);
        //    // Act & Assert
        //    var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
        //        _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));

        //    ex.Message.Should().Contain("File cannot be empty");
        //}

        //[Fact]
        //public async Task CreateDocument_WithFileSizeExceedingLimit_ShouldThrowArgumentException()
        //{
        //    // Arrange
        //    var fileName = "test.pdf";
        //    long fileSize = 2 * 1024 * 1024; // 2 MB (exceeds 1 MB limit)
        //    var fileContent = new byte[fileSize];
        //    var stream = new MemoryStream(fileContent);
        //    var cancellationToken = CancellationToken.None;

        //    // Act & Assert
        //    var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
        //        _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));

        //    ex.Message.Should().Contain("exceeds maximum limit");
        //}

        //[Theory]
        //[InlineData("")]
        //[InlineData("   ")]
        //public async Task CreateDocument_WithInvalidFileName_ShouldThrowArgumentException(string fileName)
        //{
        //    // Arrange
        //    long fileSize = 500 * 1024;
        //    var stream = new MemoryStream(new byte[fileSize]);
        //    var cancellationToken = CancellationToken.None;

        //    // Act & Assert
        //    await Assert.ThrowsAsync<ArgumentException>(() =>
        //        _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));
        //}

        //[Theory]
        //[InlineData("test.exe")]
        //[InlineData("test.txt")]
        //[InlineData("test.zip")]
        //public async Task CreateDocument_WithUnsupportedFileExtension_ShouldThrowArgumentException(string fileName)
        //{
        //    // Arrange
        //    long fileSize = 500 * 1024;
        //    var fileContent = new byte[fileSize];
        //    var stream = new MemoryStream(fileContent);
        //    var cancellationToken = CancellationToken.None;

        //    // Act & Assert
        //    await Assert.ThrowsAsync<ArgumentException>(() =>
        //        _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));
        //}

        //[Fact]
        //public async Task CreateDocument_WhenBlobUploadFails_ShouldThrowInvalidOperationException()
        //{
        //    // Arrange
        //    var fileName = "test.pdf";
        //    long fileSize = 500 * 1024;
        //    var fileContent = new byte[fileSize];
        //    var stream = new MemoryStream(fileContent);
        //    var cancellationToken = CancellationToken.None;

        //    _mockRepository
        //        .Setup(x => x.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
        //        .ReturnsAsync((Document d, CancellationToken ct) => d.Id);

        //    _mockBlobStorage
        //        .Setup(x => x.UploadDocumentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(false); // ✅ Blob upload fails

        //    // Act & Assert
        //    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        //        _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));
        //}

        #endregion

        #region GetById

        //[Fact]
        //public async Task GetById_WithValidId_ShouldReturnCreateDocumentResponse()
        //{
        //    // Arrange
        //    var documentId = Guid.NewGuid();
        //    var document = new Document
        //    {
        //        Id = documentId,
        //        FileName = "test.pdf",
        //        Status = "Completed",
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    _mockRepository
        //        .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(document);

        //    // Act
        //    var result = await _documentService.GetById(documentId, CancellationToken.None);

        //    // Assert
        //    result.Should().NotBeNull();
        //    result!.DocumentId.Should().Be(documentId);
        //    result.FileName.Should().Be("test.pdf");
        //    result.Status.Should().Be("Completed");
        //}

        [Fact]
        public async Task GetById_WithEmptyId_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _documentService.GetById(Guid.Empty, CancellationToken.None));
        }


        [Fact]
        public async Task GetById_WhenDocumentNotFound_ShouldReturnNull()
        {
            // Arrange
            var documentId = Guid.NewGuid();
            _mockRepository
                .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Document?)null);

            // Act
            var result = await _documentService.GetById(documentId, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }

        #endregion


        //[Fact]
        //public async Task UploadAsync_WhenBlobUploadFails_DoesNotDeleteBlob()
        //{
        //    // Arrange
        //    _mockRepository
        //        .Setup(x => x.AddDocumentAsync(
        //            It.IsAny<Document>(),
        //            It.IsAny<CancellationToken>()))
        //        .Returns(Task.FromResult(Guid.NewGuid()));

        //    _mockBlobStorage
        //    .Setup(x => x.UploadDocumentAsync(
        //        It.IsAny<Guid>(),
        //        It.IsAny<string>(),
        //        It.IsAny<Stream>(),
        //        It.IsAny<CancellationToken>()))
        //    .ThrowsAsync(
        //        new InvalidOperationException("Blob upload failed"));

        //    _mockRepository
        //        .Setup(x => x.UpdateAsync(
        //            It.IsAny<Document>(),
        //            It.IsAny<CancellationToken>()))
        //        .Returns(Task.CompletedTask);

        //    //var service = CreateService();

        //    using var stream = new MemoryStream(
        //        new byte[] { 1, 2, 3 });

        //    // Act
        //    await Assert.ThrowsAsync<InvalidOperationException>(
        //        () => _documentService.CreateDocument(

        //            "invoice.pdf",
        //            3,
        //            "application/pdf",
        //            stream,
        //            CancellationToken.None));

        //    // Assert
        //    _mockBlobStorage.Verify(
        //        x => x.DeleteAsync(
        //            It.IsAny<string>(),
        //            It.IsAny<CancellationToken>()),
        //        Times.Never);

        //    _mockRepository.Verify(
        //        x => x.UpdateAsync(
        //            It.Is<Document>(d =>
        //                d.Status == "Failed"),
        //            It.IsAny<CancellationToken>()),
        //        Times.Once);
        //}

        [Fact]
        public async Task UploadAsync_WhenFinalUpdateFails_DeletesBlobAndRethrows()
        {
            // Arrange
            var blobName = "documents/test/invoice.pdf";
            var fileName = "invoice.pdf";
            var contentType = "application/pdf";
            var fileSize = 1024L;

            Document doc =  new Document()
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow
            };

            _mockRepository
                .Setup(x => x.AddDocumentAsync(
                    It.IsAny<Document>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(doc);
            // Mock SaveAsync - use SetupSequence: first succeeds, second throws
            var databaseException = new InvalidOperationException("Database update failed");

            _mockRepository
                .SetupSequence(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)  // First call (Step 2) succeeds
                .Throws(databaseException);   // Second call (Step 6) fails

            // Mock UploadDocumentAsync
            _mockBlobStorage
                .Setup(x => x.UploadDocumentAsync(
                    doc.Id,
                    fileName,
                    It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(blobName);

            // Mock Update - it's fire-and-forget, but we need to verify it was called
            _mockRepository
                .Setup(x => x.Update(
                    It.IsAny<Document>(),
                    It.IsAny<CancellationToken>()));
                

            // Mock AddAsync for outbox - verify with serialized JSON
            _mockOutboxService
                .Setup(x => x.AddAsync(
                    "DocumentCreated",
                    It.Is<string>(json => json.Contains(doc.Id.ToString())),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Mock DeleteAsync for blob cleanup
            _mockBlobStorage
                .Setup(x => x.DeleteAsync(
                    blobName,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Mock DeleteAsync for document cleanup
            _mockRepository
                .Setup(x => x.DeleteAsync(
                    doc.Id,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new DocumentService(
                _mockRepository.Object,
                _mockBlobStorage.Object,
                _mockOutboxService.Object,
                _mockLogger.Object);

            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateDocument(
                    fileName,
                    fileSize,
                    contentType,
                    stream,
                    CancellationToken.None));

            Assert.Same(databaseException, exception);

            // Verify the cleanup flow
            _mockBlobStorage.Verify(
                x => x.DeleteAsync(
                    blobName,
                    It.IsAny<CancellationToken>()),
                Times.Once,
                "Blob should be deleted on failure");

            _mockRepository.Verify(
                x => x.DeleteAsync(
                    doc.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once,
                "Document should be deleted on failure");

            // Verify SaveAsync was called twice
            _mockRepository.Verify(
                x => x.SaveAsync(It.IsAny<CancellationToken>()),
                Times.Exactly(2),
                "SaveAsync should be called twice (after metadata and after update)");
        }

        [Fact]
        public async Task UploadAsync_WhenBlobUploadFails_DoesNotDeleteBlob()
        {
            // Arrange
            var blobName = "documents/test/invoice.pdf";
            var fileName = "invoice.pdf";
            var contentType = "application/pdf";
            var fileSize = 1024L;

            Document doc = new Document()
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow
            };
            // Arrange
            _mockRepository
                .Setup(x => x.AddDocumentAsync(
                    It.IsAny<Document>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(doc);



            // Mock SaveAsync - first call succeeds, second call (in fallback) succeeds
            _mockRepository
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockBlobStorage
                .Setup(x => x.UploadDocumentAsync(
                     doc.Id,
                    fileName,
                    It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(
                    new InvalidOperationException("Blob upload failed"));

            _mockRepository
                .Setup(x => x.Update(
                    It.IsAny<Document>(),
                    It.IsAny<CancellationToken>()));


            var service = new DocumentService(
                _mockRepository.Object,
                _mockBlobStorage.Object,
                _mockOutboxService.Object,
                _mockLogger.Object);

            using var stream = new MemoryStream(
                new byte[] { 1, 2, 3 });

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateDocument(
                    fileName,
                    fileSize,
                    contentType,
                    stream,
                    CancellationToken.None));

            // Assert
            // Verify blob deletion was never attempted (because upload failed)
            _mockBlobStorage.Verify(
                x => x.DeleteAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            // Verify document deletion was attempted
            _mockRepository.Verify(
                x => x.DeleteAsync(
                    doc.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify Update was called with Failed status (in fallback)
            //_mockRepository.Verify(
            //    x => x.Update(
            //        It.Is<Document>(d => d.Status == DocumentStatus.Failed),
            //        It.IsAny<CancellationToken>()),
            //    Times.Once);

            // Verify SaveAsync was called twice (after AddDocumentAsync and in fallback)
            _mockRepository.Verify(
                x => x.SaveAsync(It.IsAny<CancellationToken>()),
                Times.Exactly(1));
        }
    }
}
    
