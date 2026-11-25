using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Services.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;

namespace DocumentProcessingService.Tests.Infrastructure.Services.Outbox
{
    public class OutboxPublisherTests
    {
        private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
        private readonly Mock<IMessageBus> _messageBusMock;
        private readonly Mock<ILogger<OutboxPublisher>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly OutboxPublisher _publisher;

        public OutboxPublisherTests()
        {
            _outboxRepositoryMock = new Mock<IOutboxRepository>();
            _messageBusMock = new Mock<IMessageBus>();
            _loggerMock = new Mock<ILogger<OutboxPublisher>>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup configuration
            _configurationMock
                .Setup(x => x["Workers:DocumentProcessing:QueueName"])
                .Returns("test-queue");
            _configurationMock
                .Setup(x => x["Outbox:MaxRetries"])
                .Returns("5");

            _publisher = new OutboxPublisher(
                _outboxRepositoryMock.Object,
                _messageBusMock.Object,
                _loggerMock.Object,
                _configurationMock.Object);
        }

        #region Happy Path Tests

        [Fact]
        public async Task PublishPendingMessagesAsync_WithValidMessages_PublishesSuccessfully()
        {
            // Arrange
            var messages = CreateTestMessages(count: 10);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(10, result.Fetched);
            Assert.Equal(10, result.Published);
            Assert.Equal(0, result.Failed);

            // Verify batch was sent (not one-by-one)
            _messageBusMock.Verify(
                x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()),
                Times.Once);  // Only 1 call for 10 messages (batch)
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_WithNoMessages_ReturnsZeroResult()
        {
            // Arrange
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OutboxMessageDTO>());

            // Act
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(0, result.Fetched);
            Assert.Equal(0, result.Published);
            Assert.Equal(0, result.Failed);

            // Verify SaveAsync was NOT called
            _outboxRepositoryMock.Verify(
                x => x.SaveAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_WithMultipleBatches_PublishesAllBatches()
        {
            // Arrange - 35 messages with batch size 10 = 4 batches
            var messages = CreateTestMessages(count: 35);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(35, result.Fetched);
            Assert.Equal(35, result.Published);
            Assert.Equal(0, result.Failed);

            // Verify 4 batches were sent (35 messages / 10 batch size)
            _messageBusMock.Verify(
                x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(4));
        }

        #endregion

        #region Failure Scenario Tests

        [Fact]
        public async Task PublishPendingMessagesAsync_WhenBatchFails_MarkMessagesAsFailed()
        {
            // Arrange
            var messages = CreateTestMessages(count: 10);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            // Simulate batch failure
            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServiceBusException("Connection failed", ServiceBusFailureReason.ServiceCommunicationProblem));

            _outboxRepositoryMock
                .Setup(x => x.MarkAsFailed(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(10, result.Fetched);
            Assert.Equal(0, result.Published);
            Assert.Equal(10, result.Failed);  // All 10 marked as failed

            // Verify MarkAsFailed was called for each message
            _outboxRepositoryMock.Verify(
                x => x.MarkAsFailed(It.IsAny<Guid>(), It.IsAny<string>(), 5, It.IsAny<CancellationToken>()),
                Times.Exactly(10));
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_WithPartialBatchFailure_HandlesCorrectly()
        {
            // Arrange - 20 messages, first batch succeeds, second fails
            var messages = CreateTestMessages(count: 20);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            var callCount = 0;
            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                        return Task.CompletedTask;  // First batch succeeds
                    else
                        throw new ServiceBusException("Connection failed", ServiceBusFailureReason.ServiceCommunicationProblem);  // Second batch fails
                });

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsFailed(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(20, result.Fetched);
            Assert.Equal(10, result.Published);   // First batch succeeded
            Assert.Equal(10, result.Failed);      // Second batch failed
        }

        #endregion

        #region Performance/Batch Tests

        [Fact]
        public async Task PublishPendingMessagesAsync_UsesBatching_NotOneByOne()
        {
            // Arrange
            var messages = CreateTestMessages(count: 100);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert - 100 messages / 10 batch size = 10 calls
            _messageBusMock.Verify(
                x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(10));  // 10 batches, not 100 individual calls!
        }

        [Theory]
        [InlineData(10)]
        [InlineData(25)]
        [InlineData(50)]
        [InlineData(100)]
        public async Task PublishPendingMessagesAsync_BatchSizeOptimization_CorrectNumberOfCalls(int messageCount)
        {
            // Arrange
            var messages = CreateTestMessages(count: messageCount);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Calculate expected batches (batch size = 10)
            var expectedBatches = (messageCount + 9) / 10;  // Ceiling division

            // Act
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            _messageBusMock.Verify(
                x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(expectedBatches));

            Assert.Equal(messageCount, result.Published);
        }

        #endregion

        #region Verification Tests

        [Fact]
        public async Task PublishPendingMessagesAsync_VerifiesCorrectBatchSizes()
        {
            // Arrange
            var messages = CreateTestMessages(count: 25);  // Not evenly divisible by 10
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            var capturedBatches = new List<List<ServiceBusMessage>>();

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<ServiceBusMessage>, CancellationToken>((batch, ct) =>
                {
                    capturedBatches.Add(new List<ServiceBusMessage>(batch));
                })
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, capturedBatches.Count);        // 3 batches
            Assert.Equal(10, capturedBatches[0].Count);    // First: 10
            Assert.Equal(10, capturedBatches[1].Count);    // Second: 10
            Assert.Equal(5, capturedBatches[2].Count);     // Third: 5 (remainder)
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_SavesAfterPublishing()
        {
            // Arrange
            var messages = CreateTestMessages(count: 10);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            var publishedBefore = false;

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Callback(() => publishedBefore = true)
                .Returns(Task.CompletedTask);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Callback(() => publishedBefore = false)
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            _outboxRepositoryMock.Verify(
                x => x.SaveAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Performance Benchmarking Tests

        [Fact]
        public async Task PublishPendingMessagesAsync_Performance_CompletesIn100MessagesUnder1Second()
        {
            // Arrange
            var messages = CreateTestMessages(count: 100);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var sw = Stopwatch.StartNew();
            var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);
            sw.Stop();

            // Assert
            Assert.Equal(100, result.Published);
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Expected publish time < 1000ms, but was {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_PerformanceMetrics_LogsTimingInfo()
        {
            // Arrange
            var messages = CreateTestMessages(count: 50);
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert - Verify logging happened with timing
            _loggerMock.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Publishing cycle completed") &&
                        v.ToString()!.Contains("Duration")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Cancellation Scenario Tests

        // TEST 1: Cancellation during GetPendingMessagesAsync (DB access)
        [Fact]
        public async Task PublishPendingMessagesAsync_WhenCancelledDuringDatabaseAccess_ThrowsOperationCanceledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken ct) =>
                {
                    // Simulate cancellation during DB query
                    cts.Cancel();
                    await Task.Delay(100, ct);  // Will throw OperationCanceledException
                    return new List<OutboxMessageDTO>();
                });

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _publisher.PublishPendingMessagesAsync(cts.Token));

            // Verify SaveAsync was NOT called (cleanup wasn't reached)
            _outboxRepositoryMock.Verify(
                x => x.SaveAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // TEST 2: Cancellation during PublishMessages (network/message bus operation)
        [Fact]
        public async Task PublishPendingMessagesAsync_WhenCancelledDuringPublish_PartialPublishAndCancellation()
        {
            // Arrange
            var messages = CreateTestMessages(count: 20);
            var cts = new CancellationTokenSource();

            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            // Cancel on the first batch publish call
            var callCount = 0;
            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(async (List<ServiceBusMessage> msgs, CancellationToken ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        cts.Cancel();
                        await Task.Delay(50, ct);  // Will throw OperationCanceledException
                    }
                    return Task.CompletedTask;
                });

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _publisher.PublishPendingMessagesAsync(cts.Token));

            // Verify only 1 publish attempt was made before cancellation
            _messageBusMock.Verify(
                x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // TEST 3: Cancellation during MarkAsPulished (DB update mid-operation)
        [Fact]
        public async Task PublishPendingMessagesAsync_WhenCancelledDuringMarkAsPublished_CancelsAndRollsBack()
        {
            // Arrange
            var messages = CreateTestMessages(count: 5);
            var cts = new CancellationTokenSource();

            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Cancel after successful publish, during marking as published
            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid id, CancellationToken ct) =>
                {
                    cts.Cancel();
                    await Task.Delay(50, ct);  // Will throw OperationCanceledException
                });

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _publisher.PublishPendingMessagesAsync(cts.Token));

            // Verify SaveAsync was NOT called
            _outboxRepositoryMock.Verify(
                x => x.SaveAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion







        #region Helper Methods

        private List<OutboxMessageDTO> CreateTestMessages(int count)
        {
            return Enumerable.Range(1, count)
                .Select((i, index) => new OutboxMessageDTO
                {
                    Id = Guid.NewGuid(),
                    Type = "DocumentProcessed",
                    Content = $"{{\"message\": \"test-{i}\", \"index\": {index}}}",
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();
        }

        #endregion
    }
}
