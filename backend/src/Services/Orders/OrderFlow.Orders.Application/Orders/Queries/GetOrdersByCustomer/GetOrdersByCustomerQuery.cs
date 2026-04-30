using MediatR;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;

namespace OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

public sealed record GetOrdersByCustomerQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<IReadOnlyList<OrderSummaryDto>>>;
