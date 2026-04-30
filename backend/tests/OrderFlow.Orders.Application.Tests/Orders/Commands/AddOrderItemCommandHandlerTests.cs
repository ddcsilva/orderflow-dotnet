using FluentAssertions;
using Moq;
using OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;
using OrderFlow.Orders.Domain.Interfaces;
using OrderFlow.Orders.Domain.ValueObjects;
using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Application.Tests.Orders.Commands;

public class AddOrderItemCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AddOrderItemCommandHandler _handler;

    public AddOrderItemCommandHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new AddOrderItemCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_OrderExists_AddsItemSuccessfully()
    {
        var order = Order.Create(Guid.NewGuid(),
            Address.Create("Rua", "1", "Bairro", "Cidade", "SP", "01001000"));

        _repositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AddOrderItemCommand(order.Id, Guid.NewGuid(), "Laptop", 2500m, 1);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        order.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsFailure()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), "Laptop", 2500m, 1);

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NotFound");
    }
}
