namespace OrderFlow.Orders.Application.Common.Interfaces;

/// <summary>
/// Snapshot mínimo de produto retornado pelo Catalog API para o Orders.
/// Mantido propositalmente isolado do agregado <c>Catalog.Domain.Product</c>
/// para preservar o desacoplamento entre os bounded contexts.
/// </summary>
public sealed record ProductSnapshot(Guid Id, string Name, decimal Price, int StockQuantity)
{
    public bool HasSufficientStock(int requestedQuantity) => StockQuantity >= requestedQuantity;
}

/// <summary>
/// Cliente HTTP resiliente para o Catalog API.
/// Implementação concreta em Infrastructure usa pipeline Polly v8.
/// </summary>
public interface ICatalogClient
{
    /// <summary>
    /// Busca um produto por ID. Retorna <c>null</c> quando o produto não existe (404)
    /// ou quando a pipeline esgota retries/abre o circuit breaker (degradação graciosa).
    /// </summary>
    Task<ProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}
