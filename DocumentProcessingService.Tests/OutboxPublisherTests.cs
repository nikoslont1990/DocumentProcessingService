using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Services.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentProcessingService.Tests
{
    public class OutboxPublisherTests
    {
        [Fact]
        public async Task PublishPendingMessagesAsync_AllMessagesPublish_ReturnsAllPublished()
        {
            // Arrange
            var rec1 = new OutboxMessageDTO { Id = Guid.NewGuid(), Content = "{\"a\":1}", Type = "DocCreated" };
            var rec2 = new OutboxMessageDTO { Id = Guid.NewGuid(), Content = "{\"a\":2}", Type = "DocCreated" };
            var records = new List<OutboxMessageDTO> { rec1, rec2 };

            var repo = new Mock<IOutboxRepository>();
            repo.Setup(r => r.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(records);

            var bus = new Mock<IMessageBus>();
            bus.Setup(b => b.PublishMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            repo.Setup(r => r.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var logger = new Mock<ILogger<OutboxPublisher>>();

            var sut = new OutboxPublisher(repo.Object, bus.Object, logger.Object, config);

            // Act
            var result = await sut.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Fetched);
            Assert.Equal(2, result.Published);
            Assert.Equal(0, result.Failed);

            bus.Verify(b => b.PublishMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            repo.Verify(r => r.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            repo.Verify(r => r.MarkAsFailed(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_OnePublishFails_MarksFailed_Continues()
        {
            // Arrange
            var ok = new OutboxMessageDTO { Id = Guid.NewGuid(), Content = "{\"ok\":true}", Type = "DocCreated" };
            var bad = new OutboxMessageDTO { Id = Guid.NewGuid(), Content = "{\"bad\":true}", Type = "DocCreated" };
            var records = new List<OutboxMessageDTO> { ok, bad };

            var repo = new Mock<IOutboxRepository>();
            repo.Setup(r => r.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(records);
            repo.Setup(r => r.MarkAsPulished(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.MarkAsFailed(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var bus = new Mock<IMessageBus>();
            bus.Setup(b => b.PublishMessage(It.Is<ServiceBusMessage>(m => m.MessageId == ok.Id.ToString()), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
            bus.Setup(b => b.PublishMessage(It.Is<ServiceBusMessage>(m => m.MessageId == bad.Id.ToString()), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new Exception("broker error"));

            var config = new ConfigurationBuilder().AddInMemoryCollection().Build(); // no Outbox:MaxRetries => default 5
            var logger = new Mock<ILogger<OutboxPublisher>>();

            var sut = new OutboxPublisher(repo.Object, bus.Object, logger.Object, config);

            // Act
            var result = await sut.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Fetched);
            Assert.Equal(1, result.Published);
            Assert.Equal(1, result.Failed);

            repo.Verify(r => r.MarkAsFailed(
                bad.Id,
                It.IsAny<string>(),
                5,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_NoPendingMessages_ReturnsZeroes()
        {
            // Arrange
            var repo = new Mock<IOutboxRepository>();
            repo.Setup(r => r.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OutboxMessageDTO>());
            repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var bus = new Mock<IMessageBus>();
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var logger = new Mock<ILogger<OutboxPublisher>>();

            var sut = new OutboxPublisher(repo.Object, bus.Object, logger.Object, config);

            // Act
            var result = await sut.PublishPendingMessagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(0, result.Fetched);
            Assert.Equal(0, result.Published);
            Assert.Equal(0, result.Failed);

            bus.Verify(b => b.PublishMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Never);
            repo.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_RetriesUntilMaxRetries_ThenNoLongerProcessed()
        {
            // Arrange
            var msg = new OutboxMessageDTO
            {
                Id = Guid.NewGuid(),
                Content = """{"docId":"1"}""",
                Type = "DocumentUploaded"
            };

            const int maxRetries = 3;
            var configData = new Dictionary<string, string?>
            {
                ["Outbox:MaxRetries"] = maxRetries.ToString()
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var repo = new Mock<IOutboxRepository>();
            var bus = new Mock<IMessageBus>();
            var logger = new Mock<ILogger<OutboxPublisher>>();

            // Simulate repository behavior:
            // message appears as "pending" only first 3 cycles, then removed (max retries reached).
            var cycle = 0;
            repo.Setup(r => r.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    cycle++;
                    return cycle <= maxRetries
                        ? new List<OutboxMessageDTO> { msg }
                        : new List<OutboxMessageDTO>();
                });

            bus.Setup(b => b.PublishMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new Exception("Service Bus unavailable"));

            repo.Setup(r => r.MarkAsFailed(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sut = new OutboxPublisher(repo.Object, bus.Object, logger.Object, configuration);

            // Act: run more cycles than max retries
            for (int i = 0; i < maxRetries + 2; i++)
            {
                await sut.PublishPendingMessagesAsync(CancellationToken.None);
            }

            // Assert
            bus.Verify(
                b => b.PublishMessage(It.Is<ServiceBusMessage>(m => m.MessageId == msg.Id.ToString()), It.IsAny<CancellationToken>()),
                Times.Exactly(maxRetries));

            repo.Verify(
                r => r.MarkAsFailed(msg.Id, It.IsAny<string>(), maxRetries, It.IsAny<CancellationToken>()),
                Times.Exactly(maxRetries));
        }


        [Fact]
        public async Task PublishPendingMessagesAsync_FailsFourTimes_ThenSucceeds_OnFifthAttempt()
        {
            // Arrange
            var msg = new OutboxMessageDTO
            {
                Id = Guid.NewGuid(),
                Content = """{"docId":"123"}""",
                Type = "DocumentUploaded"
            };

            var configData = new Dictionary<string, string?>
            {
                ["Outbox:MaxRetries"] = "5"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var repo = new Mock<IOutboxRepository>();
            var bus = new Mock<IMessageBus>();
            var logger = new Mock<ILogger<OutboxPublisher>>();

            var publishAttempts = 0;
            var isPublished = false;

            // Keep returning the message until it's marked published
            repo.Setup(r => r.GetPendingMessagesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => isPublished
                    ? new List<OutboxMessageDTO>()
                    : new List<OutboxMessageDTO> { msg });

            bus.Setup(b => b.PublishMessage(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
               .Returns(() =>
               {
                   publishAttempts++;
                   if (publishAttempts <= 4)
                       throw new Exception("Transient broker failure");

                   return Task.CompletedTask; // 5th attempt succeeds
               });

            repo.Setup(r => r.MarkAsFailed(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            repo.Setup(r => r.MarkAsPulished(msg.Id, It.IsAny<CancellationToken>()))
                .Callback(() => isPublished = true)
                .Returns(Task.CompletedTask);

            repo.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sut = new OutboxPublisher(repo.Object, bus.Object, logger.Object, configuration);

            // Act: simulate worker cycles
            for (int i = 0; i < 6; i++)
            {
                await sut.PublishPendingMessagesAsync(CancellationToken.None);
                if (isPublished) break;
            }

            // Assert
            Assert.True(isPublished);
            Assert.Equal(5, publishAttempts);

            repo.Verify(r => r.MarkAsFailed(msg.Id, It.IsAny<string>(), 5, It.IsAny<CancellationToken>()), Times.Exactly(4));
            repo.Verify(r => r.MarkAsPulished(msg.Id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
