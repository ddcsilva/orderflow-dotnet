using MassTransit;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Notifications.Worker.Idempotency;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCancelledConsumer(
    IIdempotencyStore idempotencyStore,
    ILogger<OrderCancelledConsumer> logger)
    : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var messageKey = $"{nameof(OrderCancelled)}:{context.MessageId ?? Guid.NewGuid()}";
        if (!await idempotencyStore.TryMarkAsProcessedAsync(messageKey, context.CancellationToken))
        {
            logger.LogWarning(
                "Duplicate OrderCancelled message ignored. MessageId={MessageId}, OrderId={OrderId}",
                context.MessageId, context.Message.OrderId);
            return;
        }

        var message = context.Message;
        logger.LogInformation(
            "Processing OrderCancelled: OrderId={OrderId}, Reason={Reason}",
            message.OrderId, message.Reason);

        await Task.Delay(50, context.CancellationToken);

        logger.LogInformation(
            "Cancellation notification sent for order {OrderId}", message.OrderId);
    }
}
