using MassTransit;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Notifications.Worker.Idempotency;

namespace OrderFlow.Notifications.Worker.Consumers;

public sealed class OrderCreatedConsumer(
    IIdempotencyStore idempotencyStore,
    ILogger<OrderCreatedConsumer> logger)
    : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var messageKey = $"{nameof(OrderCreated)}:{context.MessageId ?? Guid.NewGuid()}";
        if (!await idempotencyStore.TryMarkAsProcessedAsync(messageKey, context.CancellationToken))
        {
            logger.LogWarning(
                "Duplicate OrderCreated message ignored. MessageId={MessageId}, OrderId={OrderId}",
                context.MessageId, context.Message.OrderId);
            return;
        }

        var message = context.Message;
        logger.LogInformation(
            "Processing OrderCreated: OrderId={OrderId}, OrderNumber={OrderNumber}, CustomerId={CustomerId}",
            message.OrderId, message.OrderNumber, message.CustomerId);

        // Simulação: em produção, buscar email do customer e disparar notificação real.
        await Task.Delay(50, context.CancellationToken);

        logger.LogInformation(
            "Notification sent for new order {OrderNumber}", message.OrderNumber);
    }
}
