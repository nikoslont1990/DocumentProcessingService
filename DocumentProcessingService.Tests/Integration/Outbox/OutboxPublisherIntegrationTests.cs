using Azure.Messaging.ServiceBus;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Infrastructure.Pesistence;
using DocumentProcessingService.Infrastructure.Pesistence.Outbox;
using DocumentProcessingService.Infrastructure.Services.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;


namespace DocumentProcessingService.Tests.Integration.Outbox
{
    public class OutboxPublisherIntegrationTests
    {
        [Fact]
        public async Task PublishPendingMessagesAsync_Fails4Times_ThenSucceeds_PersistsCorrectState()
        {
            await using var connection = await OpenInMemorySqliteAsync();
            var dbOptions = CreateDbOptions(connection);

            await using var db = new DocumentDbContext(dbOptions);
            await db.Database.EnsureCreatedAsync();

            var messageId = Guid.NewGuid();
            await SeedAsync(db, new OutboxMessage
            {
                Id = messageId,
                Type = "DocumentUploaded",
                Content = """{"docId":"123"}""",
                CreatedAt = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending
            });

            var bus = new FailThenSucceedMessageBus(failCount: 4);
            var sut = CreateSut(db, bus, maxRetries: 5);

            for (var cycle = 0; cycle < 8; cycle++)
            {
                await sut.PublishPendingMessagesAsync(CancellationToken.None);

                var current = await db.OutboxMessages.SingleAsync(x => x.Id == messageId);
                if (current.Status == OutboxMessageStatus.Published)
                    break;

                await ForceRetryableAsync(db, messageId);
            }

            var finalState = await db.OutboxMessages.SingleAsync(x => x.Id == messageId);

            Assert.Equal(5, bus.Attempts); // 4 failed + 1 success
            Assert.Equal(4, finalState.RetryCount);
            Assert.Equal(OutboxMessageStatus.Published, finalState.Status);
            Assert.NotNull(finalState.PublishedAt);
        }

        [Fact]
        public async Task PublishPendingMessagesAsync_MixedOutcomes_FirstPublished_SecondFailedAtMaxRetries_ThirdStillPending()
        {
            await using var connection = await OpenInMemorySqliteAsync();
            var dbOptions = CreateDbOptions(connection);

            await using var db = new DocumentDbContext(dbOptions);
            await db.Database.EnsureCreatedAsync();

            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var thirdId = Guid.NewGuid();

            await SeedAsync(db,
                new OutboxMessage
                {
                    Id = firstId,
                    Type = "Doc",
                    Content = """{"n":1}""",
                    CreatedAt = DateTime.UtcNow.AddSeconds(-3),
                    Status = OutboxMessageStatus.Pending
                },
                new OutboxMessage
                {
                    Id = secondId,
                    Type = "Doc",
                    Content = """{"n":2}""",
                    CreatedAt = DateTime.UtcNow.AddSeconds(-2),
                    Status = OutboxMessageStatus.Pending
                },
                new OutboxMessage
                {
                    Id = thirdId,
                    Type = "Doc",
                    Content = """{"n":3}""",
                    CreatedAt = DateTime.UtcNow.AddSeconds(-1),
                    Status = OutboxMessageStatus.Pending
                });

            var bus = new MixedBehaviorMessageBus(firstId, secondId, thirdId);
            var sut = CreateSut(db, bus, maxRetries: 3);

            // Cycle 1:
            // first -> success, second -> fail, third -> fail once
            await sut.PublishPendingMessagesAsync(CancellationToken.None);

            // Push only second to max retries (third remains pending with future NextRetryAt)
            await ForceRetryableAsync(db, secondId);
            await sut.PublishPendingMessagesAsync(CancellationToken.None);

            await ForceRetryableAsync(db, secondId);
            await sut.PublishPendingMessagesAsync(CancellationToken.None);

            var first = await db.OutboxMessages.SingleAsync(x => x.Id == firstId);
            var second = await db.OutboxMessages.SingleAsync(x => x.Id == secondId);
            var third = await db.OutboxMessages.SingleAsync(x => x.Id == thirdId);

            Assert.Equal(OutboxMessageStatus.Published, first.Status);
            Assert.NotNull(first.PublishedAt);

            Assert.Equal(OutboxMessageStatus.Failed, second.Status);
            Assert.Equal(3, second.RetryCount);
            Assert.Null(second.PublishedAt);

            // "still processing" in current model = Pending + retry scheduled in future
            Assert.Equal(OutboxMessageStatus.Published, third.Status);
            Assert.Equal(1, third.RetryCount);
            Assert.True(third.NextRetryAt > DateTime.UtcNow);
            Assert.Null(third.PublishedAt);
        }

        // ---------- Helpers ----------

        private static async Task<SqliteConnection> OpenInMemorySqliteAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            return connection;
        }

        private static DbContextOptions<DocumentDbContext> CreateDbOptions(SqliteConnection connection) =>
            new DbContextOptionsBuilder<DocumentDbContext>()
                .UseSqlite(connection)
                .Options;

        private static OutboxPublisher CreateSut(DocumentDbContext db, IMessageBus bus, int maxRetries)
        {
            var repository = new OutboxRepository(db);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Outbox:MaxRetries"] = maxRetries.ToString()
                })
                .Build();

            return new OutboxPublisher(
                repository,
                bus,
                NullLogger<OutboxPublisher>.Instance,
                config);
        }

        private static async Task SeedAsync(DocumentDbContext db, params OutboxMessage[] messages)
        {
            db.OutboxMessages.AddRange(messages);
            await db.SaveChangesAsync();
        }

        private static async Task ForceRetryableAsync(DocumentDbContext db, Guid id)
        {
            var row = await db.OutboxMessages.SingleAsync(x => x.Id == id);
            row.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        // ---------- Test doubles ----------

        private sealed class FailThenSucceedMessageBus : IMessageBus
        {
            private readonly int _failCount;
            public int Attempts { get; private set; }

            public FailThenSucceedMessageBus(int failCount) => _failCount = failCount;

            public Task PublishMessage(ServiceBusMessage message, CancellationToken cancellationToken)
            {
                Attempts++;
                if (Attempts <= _failCount)
                    throw new Exception("Simulated transient failure");
                return Task.CompletedTask;
            }

            public Task PublishMessages(IEnumerable<ServiceBusMessage> message, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class MixedBehaviorMessageBus : IMessageBus
        {
            private readonly Guid _firstId;
            private readonly Guid _secondId;
            private readonly Guid _thirdId;
            private int _thirdAttempts;

            public MixedBehaviorMessageBus(Guid firstId, Guid secondId, Guid thirdId)
            {
                _firstId = firstId;
                _secondId = secondId;
                _thirdId = thirdId;
            }

            public Task PublishMessage(ServiceBusMessage message, CancellationToken cancellationToken)
            {
                var id = Guid.Parse(message.MessageId);

                if (id == _firstId)
                    return Task.CompletedTask;      // always succeeds

                if (id == _secondId)
                    throw new Exception("Always fail second");

                if (id == _thirdId)
                {
                    _thirdAttempts++;
                    if (_thirdAttempts == 1)
                        throw new Exception("Fail third once");
                    return Task.CompletedTask;
                }

                return Task.CompletedTask;
            }

            public Task PublishMessages(IEnumerable<ServiceBusMessage> message, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
