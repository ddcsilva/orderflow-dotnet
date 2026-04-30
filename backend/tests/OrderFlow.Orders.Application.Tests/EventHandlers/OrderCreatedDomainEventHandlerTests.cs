using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderFlow.Contracts.IntegrationEvents;
using OrderFlow.Orders.Application.Orders.EventHandlers;
using OrderFlow.Orders.Domain.Events;

namespace OrderFlow.Orders.Application.Tests.EventHandlers;

public class OrderCreatedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_DomainEvent_PublishesIntegrationEvent()
    {
        var publishMock = new Mock<IPublishEndpoint>();
        var handler = new OrderCreatedDomainEventHandler(
            publishMock.Object,
            NullLogger<OrderCreatedDomainEventHandler>.Instance);

        var domainEvent = new OrderCreatedDomainEvent(
            Guid.NewGuid(), "ORD-20260415-XYZ", Guid.NewGuid(), 0m);

        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        publishMock.Verify(p => p.Publish(
            It.Is<OrderCreated>(e =>
                e.OrderId == domainEvent.OrderId &&
                e.OrderNumber == domainEvent.OrderNumber &&
                e.CustomerId == domainEvent.CustomerId &&
                e.TotalAmount == domainEvent.TotalAmount),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullNotification_Throws()
    {
        var handler = new OrderCreatedDomainEventHandler(
            Mock.Of<IPublishEndpoint>(),
            NullLogger<OrderCreatedDomainEventHandler>.Instance);

        var act = async () => await handler.Handle(null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
