using System.Globalization;
using MassTransit;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Notifications.Worker.Idempotency;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderConfirmedConsumer(
    IIdempotencyStore idempotencyStore,
    ILogger<OrderConfirmedConsumer> logger)
    : IConsumer<OrderConfirmed>
{
    public async Task Consume(ConsumeContext<OrderConfirmed> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var messageKey = $"{nameof(OrderConfirmed)}:{context.MessageId ?? Guid.NewGuid()}";
        if (!await idempotencyStore.TryMarkAsProcessedAsync(messageKey, context.CancellationToken))
        {
            logger.LogWarning(
                "Duplicate OrderConfirmed message ignored. MessageId={MessageId}, OrderId={OrderId}",
                context.MessageId, context.Message.OrderId);
            return;
        }

        var message = context.Message;
        logger.LogInformation(
            "Processing OrderConfirmed: OrderId={OrderId}, Total={TotalAmount}",
            message.OrderId,
            message.TotalAmount.ToString("C", CultureInfo.InvariantCulture));

        await Task.Delay(50, context.CancellationToken);

        logger.LogInformation(
            "Confirmation notification sent for order {OrderId}", message.OrderId);
    }
}
