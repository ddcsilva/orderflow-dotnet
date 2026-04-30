using FluentAssertions;
using OrderFlow.Notifications.Worker.Idempotency;

namespace OrderFlow.Notifications.Worker.Tests.Idempotency;

public class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task TryMarkAsProcessed_FirstCall_ReturnsTrue()
    {
        var store = new InMemoryIdempotencyStore();
        var result = await store.TryMarkAsProcessedAsync("key-1", TestContext.Current.CancellationToken);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryMarkAsProcessed_SecondCallSameKey_ReturnsFalse()
    {
        var store = new InMemoryIdempotencyStore();
        await store.TryMarkAsProcessedAsync("key-2", TestContext.Current.CancellationToken);
        var result = await store.TryMarkAsProcessedAsync("key-2", TestContext.Current.CancellationToken);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryMarkAsProcessed_DifferentKeys_BothReturnTrue()
    {
        var store = new InMemoryIdempotencyStore();
        var a = await store.TryMarkAsProcessedAsync("a", TestContext.Current.CancellationToken);
        var b = await store.TryMarkAsProcessedAsync("b", TestContext.Current.CancellationToken);
        a.Should().BeTrue();
        b.Should().BeTrue();
    }
}
