using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Orders.EventHandlers;

public sealed class OrderCreatedDomainEventHandler(
    ILogger<OrderCreatedDomainEventHandler> logger)
    : INotificationHandler<OrderCreatedDomainEvent>
{
    public Task Handle(OrderCreatedDomainEvent notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        logger.LogInformation(
            "Domain Event: Order created. OrderId={OrderId}, OrderNumber={OrderNumber}, CustomerId={CustomerId}",
            notification.OrderId, notification.OrderNumber, notification.CustomerId);

        return Task.CompletedTask;
    }
}
