using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderCancelledDomainEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<OrderCancelledDomainEventHandler> logger)
    : INotificationHandler<OrderCancelledDomainEvent>
{
    public async Task Handle(OrderCancelledDomainEvent notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        logger.LogInformation(
            "Domain Event: Order cancelled. Publishing OrderCancelled integration event. OrderId={OrderId}, Reason={Reason}",
            notification.OrderId, notification.Reason);

        await publishEndpoint.Publish(new OrderCancelled
        {
            OrderId = notification.OrderId,
            OrderNumber = notification.OrderNumber,
            Reason = notification.Reason,
            OccurredOn = notification.OccurredOn
        }, ct).ConfigureAwait(false);
    }
}
