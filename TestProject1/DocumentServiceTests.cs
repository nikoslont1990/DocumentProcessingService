//using DocumentProcessingService.Application.Interfaces;
//using DocumentProcessingService.Application.Services;
//using DocumentProcessingService.Domain.Entities;
//using FluentAssertions;
//using Moq;


//namespace DocumentProcessingService.Tests
//{
//    public class DocumentServiceTests
//    {
//        private readonly Mock<IDocumentRepository> _mockRepository;
//        private readonly Mock<IBlobStorageService> _mockBlobStorage;

//        private readonly DocumentService _documentService;

//        // Constructor - Setup mocks for each test
//        public DocumentServiceTests()
//        {
//            _mockRepository = new Mock<IDocumentRepository>();
//            _mockBlobStorage = new Mock<IBlobStorageService>();
//            _documentService = new DocumentService(_mockRepository.Object, _mockBlobStorage.Object);
//        }

//        #region CreateDocument - Happy Path Tests

//        [Fact]
//        public async Task CreateDocument_WithValidInputs_ShouldReturnDocument()
//        {
//            // Arrange
//            var documentId = Guid.NewGuid();
//            var fileName = "test.pdf";
//            long fileSize = 500 * 1024; // 500 KB
//            var cancellationToken = CancellationToken.None;

//            var fileContent = new byte[fileSize];
//            using (var stream = new MemoryStream(fileContent))
//            {
//                _mockRepository
//                .Setup(x => x.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(documentId);

//                _mockBlobStorage
//                        .Setup(x => x.UploadDocumentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
//                        .ReturnsAsync(true);

//                // Act
//                var result = await _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken);

//                // Assert
//                result.Should().NotBeNull();
//                result.FileName.Should().Be(fileName);
//                result.Status.Should().Be("Completed");
//            }
//        }

//        #endregion

//        #region CreateDocument - Edge Cases

//        [Fact]
//        public async Task CreateDocument_WithZeroFileSize_ShouldThrowArgumentException()
//        {
//            // Arrange
//            var documentId = Guid.NewGuid();
//            var fileName = "test.pdf";
//            long fileSize = 0; // ✅ Invalid size
//            var stream = new MemoryStream();
//            var cancellationToken = CancellationToken.None;

//            _mockRepository
//              .Setup(x => x.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
//              .ReturnsAsync(documentId);
//            // Act & Assert
//            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
//                _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));

//            ex.Message.Should().Contain("File cannot be empty");
//        }

//        [Fact]
//        public async Task CreateDocument_WithFileSizeExceedingLimit_ShouldThrowArgumentException()
//        {
//            // Arrange
//            var fileName = "test.pdf";
//            long fileSize = 2 * 1024 * 1024; // 2 MB (exceeds 1 MB limit)
//            var fileContent = new byte[fileSize];
//            var stream = new MemoryStream(fileContent);
//            var cancellationToken = CancellationToken.None;

//            // Act & Assert
//            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
//                _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));

//            ex.Message.Should().Contain("exceeds maximum limit");
//        }

//        [Theory]
//        [InlineData("")]
//        [InlineData("   ")]
//        public async Task CreateDocument_WithInvalidFileName_ShouldThrowArgumentException(string fileName)
//        {
//            // Arrange
//            long fileSize = 500 * 1024;
//            var stream = new MemoryStream(new byte[fileSize]);
//            var cancellationToken = CancellationToken.None;

//            // Act & Assert
//            await Assert.ThrowsAsync<ArgumentException>(() =>
//                _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));
//        }

//        [Theory]
//        [InlineData("test.exe")]
//        [InlineData("test.txt")]
//        [InlineData("test.zip")]
//        public async Task CreateDocument_WithUnsupportedFileExtension_ShouldThrowArgumentException(string fileName)
//        {
//            // Arrange
//            long fileSize = 500 * 1024;
//            var fileContent = new byte[fileSize];
//            var stream = new MemoryStream(fileContent);
//            var cancellationToken = CancellationToken.None;

//            // Act & Assert
//            await Assert.ThrowsAsync<ArgumentException>(() =>
//                _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));
//        }

//        [Fact]
//        public async Task CreateDocument_WhenBlobUploadFails_ShouldThrowInvalidOperationException()
//        {
//            // Arrange
//            var fileName = "test.pdf";
//            long fileSize = 500 * 1024;
//            var fileContent = new byte[fileSize];
//            var stream = new MemoryStream(fileContent);
//            var cancellationToken = CancellationToken.None;

//            _mockRepository
//                .Setup(x => x.AddDocumentAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync((Document d, CancellationToken ct) => d.Id);

//            _mockBlobStorage
//                .Setup(x => x.UploadDocumentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(false); // ✅ Blob upload fails

//            // Act & Assert
//            await Assert.ThrowsAsync<InvalidOperationException>(() =>
//                _documentService.CreateDocument(fileName, fileSize, stream, cancellationToken));
//        }

//        #endregion

//        #region GetById

//        [Fact]
//        public async Task GetById_WithValidId_ShouldReturnCreateDocumentResponse()
//        {
//            // Arrange
//            var documentId = Guid.NewGuid();
//            var document = new Document
//            {
//                Id = documentId,
//                FileName = "test.pdf",
//                Status = "Completed",
//                CreatedAt = DateTime.UtcNow
//            };

//            _mockRepository
//                .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
//                .ReturnsAsync(document);

//            // Act
//            var result = await _documentService.GetById(documentId, CancellationToken.None);

//            // Assert
//            result.Should().NotBeNull();
//            result!.DocumentId.Should().Be(documentId);
//            result.FileName.Should().Be("test.pdf");
//            result.Status.Should().Be("Completed");
//        }

//        [Fact]
//        public async Task GetById_WithEmptyId_ShouldThrowArgumentException()
//        {
//            // Act & Assert
//            await Assert.ThrowsAsync<ArgumentException>(() =>
//                _documentService.GetById(Guid.Empty, CancellationToken.None));
//        }

//        [Fact]
//        public async Task GetById_WhenDocumentNotFound_ShouldReturnNull()
//        {
//            // Arrange
//            var documentId = Guid.NewGuid();
//            _mockRepository
//                .Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
//                .ReturnsAsync((Document?)null);

//            // Act
//            var result = await _documentService.GetById(documentId, CancellationToken.None);

//            // Assert
//            result.Should().BeNull();
//        }

//        #endregion
//    }
//}
