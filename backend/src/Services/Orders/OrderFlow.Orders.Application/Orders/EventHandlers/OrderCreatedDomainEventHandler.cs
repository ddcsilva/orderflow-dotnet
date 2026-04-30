using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderCreatedDomainEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<OrderCreatedDomainEventHandler> logger)
    : INotificationHandler<OrderCreatedDomainEvent>
{
    public async Task Handle(OrderCreatedDomainEvent notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        logger.LogInformation(
            "Domain Event: Order created. Publishing OrderCreated integration event. OrderId={OrderId}, OrderNumber={OrderNumber}, CustomerId={CustomerId}",
            notification.OrderId, notification.OrderNumber, notification.CustomerId);

        await publishEndpoint.Publish(new OrderCreated
        {
            OrderId = notification.OrderId,
            OrderNumber = notification.OrderNumber,
            CustomerId = notification.CustomerId,
            TotalAmount = notification.TotalAmount,
            OccurredOn = notification.OccurredOn
        }, ct).ConfigureAwait(false);
    }
}
