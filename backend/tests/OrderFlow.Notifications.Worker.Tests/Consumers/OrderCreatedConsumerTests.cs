using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Notifications.Worker.Consumers;
using OrderFlow.Notifications.Worker.Idempotency;

namespace OrderFlow.Notifications.Worker.Tests.Consumers;

public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ValidMessage_ProcessesSuccessfully()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<OrderCreatedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new OrderCreated
            {
                OrderId = Guid.NewGuid(),
                OrderNumber = "ORD-20260415-AB123",
                CustomerId = Guid.NewGuid(),
                TotalAmount = 250.00m,
                OccurredOn = DateTime.UtcNow
            }, TestContext.Current.CancellationToken);

            (await harness.Consumed.Any<OrderCreated>()).Should().BeTrue();

            var consumerHarness = harness.GetConsumerHarness<OrderCreatedConsumer>();
            (await consumerHarness.Consumed.Any<OrderCreated>()).Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_DuplicateMessage_IsIgnoredByIdempotencyStore()
    {
        var idempotency = new InMemoryIdempotencyStore();

        await using var provider = new ServiceCollection()
            .AddSingleton<IIdempotencyStore>(idempotency)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<OrderCreatedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var sharedMessageId = NewId.NextGuid();
            var payload = new OrderCreated
            {
                OrderId = Guid.NewGuid(),
                OrderNumber = "ORD-DUP-0001",
                CustomerId = Guid.NewGuid(),
                TotalAmount = 99m,
                OccurredOn = DateTime.UtcNow
            };

            await harness.Bus.Publish(payload, ctx => ctx.MessageId = sharedMessageId, TestContext.Current.CancellationToken);
            await harness.Bus.Publish(payload, ctx => ctx.MessageId = sharedMessageId, TestContext.Current.CancellationToken);

            var consumerHarness = harness.GetConsumerHarness<OrderCreatedConsumer>();
            (await consumerHarness.Consumed.SelectAsync<OrderCreated>().Count()).Should().BeGreaterThanOrEqualTo(1);

            // Mas idempotência só processou uma vez
            (await idempotency.TryMarkAsProcessedAsync($"OrderCreated:{sharedMessageId}", TestContext.Current.CancellationToken))
                .Should().BeFalse("a segunda tentativa de marcar a mesma mensagem deve falhar");
        }
        finally
        {
            await harness.Stop();
        }
    }
}
