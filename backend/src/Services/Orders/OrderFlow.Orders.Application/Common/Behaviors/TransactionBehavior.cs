using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Orders.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(
    DbContext dbContext,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        if (!requestName.EndsWith("Command", StringComparison.Ordinal))
            return await next(ct);

        if (dbContext.Database.CurrentTransaction is not null)
            return await next(ct);

        IDbContextTransaction? transaction = null;

        try
        {
            transaction = await dbContext.Database.BeginTransactionAsync(ct);
            logger.LogInformation("Begin transaction for {RequestName} ({TransactionId})",
                requestName, transaction.TransactionId);

            var response = await next(ct);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Transaction committed for {RequestName} ({TransactionId})",
                requestName, transaction.TransactionId);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transaction rolled back for {RequestName}", requestName);

            if (transaction is not null)
                await transaction.RollbackAsync(ct);

            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }
}
