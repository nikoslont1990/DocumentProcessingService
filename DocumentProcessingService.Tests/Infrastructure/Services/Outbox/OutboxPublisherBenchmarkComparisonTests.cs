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
    public class OutboxPublisherComparisonTests
    {
        private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
        private readonly Mock<IMessageBus> _messageBusMock;
        private readonly Mock<ILogger<OutboxPublisher>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly OutboxPublisher _publisher;

        public OutboxPublisherComparisonTests()
        {
            _outboxRepositoryMock = new Mock<IOutboxRepository>();
            _messageBusMock = new Mock<IMessageBus>();
            _loggerMock = new Mock<ILogger<OutboxPublisher>>();
            _configurationMock = new Mock<IConfiguration>();

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

        [Fact]
        public async Task Batch_IsFasterThan_OneByOne()
        {
            // Arrange
            var messages = CreateTestMessages(100);
            var batchCallCount = 0;
            var oneByOneCallCount = 0;

            // ==================== BATCH PUBLISHING ====================
            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _messageBusMock
                .Setup(x => x.PublishMessages(It.IsAny<List<ServiceBusMessage>>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<ServiceBusMessage> msgs, CancellationToken ct) =>
                {
                    batchCallCount++;
                })
                .Returns(async () => await Task.Delay(10));

            var batchStopwatch = Stopwatch.StartNew();
            var batchResult = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);
            batchStopwatch.Stop();
            var timeElapsesd = batchStopwatch.ElapsedMilliseconds;

            // ==================== ONE-BY-ONE PUBLISHING ====================
            _outboxRepositoryMock.Reset();
            _messageBusMock.Reset();
            oneByOneCallCount = 0;

            _outboxRepositoryMock
                .Setup(x => x.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(messages);

            _outboxRepositoryMock
                .Setup(x => x.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _outboxRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _messageBusMock
                .Setup(x => x.PublishMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
                .Callback((ServiceBusMessage msg, CancellationToken ct) =>
                {
                    oneByOneCallCount++;
                })
                .Returns(async () => await Task.Delay(10));

            var oneByOneStopwatch = Stopwatch.StartNew();
            var oneByOneResult = await _publisher.PublishPendingMessagesOneByOne(CancellationToken.None);
            oneByOneStopwatch.Stop();
            var timeElapsedOneByOne = oneByOneStopwatch.ElapsedMilliseconds;

            // ==================== ASSERTIONS ====================
            Assert.Equal(10, batchCallCount);
            Assert.Equal(100, oneByOneCallCount);
            Assert.True(batchStopwatch.ElapsedMilliseconds < oneByOneStopwatch.ElapsedMilliseconds);
            Assert.True((double)oneByOneStopwatch.ElapsedMilliseconds / batchStopwatch.ElapsedMilliseconds > 5.0);
            Assert.Equal(100, batchResult.Published);
            Assert.Equal(100, oneByOneResult.Published);
            Assert.Equal(0, batchResult.Failed);
            Assert.Equal(0, oneByOneResult.Failed);
        }

        private List<OutboxMessageDTO> CreateTestMessages(int count)
        {
            return Enumerable.Range(1, count)
                .Select(i => new OutboxMessageDTO
                {
                    Id = Guid.NewGuid(),
                    Type = "DocumentProcessed",
                    Content = $"{{\"message\": \"test-{i}\"}}",
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();
        }
    }
}
