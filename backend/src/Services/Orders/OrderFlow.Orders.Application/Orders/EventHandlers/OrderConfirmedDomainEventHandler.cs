using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderConfirmedDomainEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<OrderConfirmedDomainEventHandler> logger)
    : INotificationHandler<OrderConfirmedDomainEvent>
{
    public async Task Handle(OrderConfirmedDomainEvent notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        logger.LogInformation(
            "Domain Event: Order confirmed. Publishing OrderConfirmed integration event. OrderId={OrderId}",
            notification.OrderId);

        await publishEndpoint.Publish(new OrderConfirmed
        {
            OrderId = notification.OrderId,
            OrderNumber = notification.OrderNumber,
            TotalAmount = notification.TotalAmount,
            OccurredOn = notification.OccurredOn
        }, ct).ConfigureAwait(false);
    }
}
