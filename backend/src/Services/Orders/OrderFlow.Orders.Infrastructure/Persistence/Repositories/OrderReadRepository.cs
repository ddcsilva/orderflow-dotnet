using System.Data;
using Dapper;
using OrderFlow.Orders.Application.Common.Interfaces;
using OrderFlow.Orders.Application.Orders.Queries.GetOrderById;
using OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

namespace OrderFlow.Orders.Infrastructure.Persistence.Repositories;

public sealed class OrderReadRepository(IDbConnection dbConnection) : IOrderReadRepository
{
    public async Task<OrderDetailDto?> GetOrderDetailAsync(Guid orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id, o.OrderNumber, o.CustomerId, o.Status,
                o.TotalAmount, o.CancellationReason,
                CONCAT(o.ShippingStreet, ', ', o.ShippingNumber, ' - ',
                       o.ShippingNeighborhood, ', ', o.ShippingCity, '/',
                       o.ShippingState) AS ShippingAddress,
                o.CreatedAt, o.UpdatedAt
            FROM Orders o
            WHERE o.Id = @OrderId;

            SELECT
                i.Id, i.ProductId, i.ProductName,
                i.UnitPrice, i.Quantity,
                (i.UnitPrice * i.Quantity) AS TotalPrice
            FROM OrderItems i
            WHERE i.OrderId = @OrderId;
            """;

        var command = new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: ct);

        using var multi = await dbConnection.QueryMultipleAsync(command);

        var order = await multi.ReadSingleOrDefaultAsync<OrderDetailDto>();
        if (order is null)
            return null;

        var items = (await multi.ReadAsync<OrderItemDto>()).ToList();
        return order with { Items = items };
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
        Guid customerId, int page, int pageSize, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id, o.OrderNumber, o.Status, o.TotalAmount,
                (SELECT COUNT(*) FROM OrderItems i WHERE i.OrderId = o.Id) AS ItemCount,
                o.CreatedAt
            FROM Orders o
            WHERE o.CustomerId = @CustomerId
            ORDER BY o.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var command = new CommandDefinition(sql, new
        {
            CustomerId = customerId,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        }, cancellationToken: ct);

        var orders = await dbConnection.QueryAsync<OrderSummaryDto>(command);

        return orders.ToList().AsReadOnly();
    }
}
