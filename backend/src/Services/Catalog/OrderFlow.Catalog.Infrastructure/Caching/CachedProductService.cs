using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;

namespace OrderFlow.Catalog.Infrastructure.Caching;

/// <summary>
/// Decorator que adiciona caching distribuído (Redis) ao IProductService.
/// Estratégia: Cache-Aside com invalidação versionada para SearchAsync.
/// </summary>
public sealed partial class CachedProductService(
    IProductService inner,
    IDistributedCache cache,
    ILogger<CachedProductService> logger) : IProductService
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Debug, Message = "Cache HIT {Key}")]
    private partial void LogCacheHit(string key);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Debug, Message = "Cache MISS {Key}")]
    private partial void LogCacheMiss(string key);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Warning, Message = "Falha de cache em {Key}")]
    private partial void LogCacheFailure(Exception ex, string key);

    private const string ProductByIdPrefix = "product:id:";
    private const string ProductBySkuPrefix = "product:sku:";
    private const string SearchPrefix = "products:search:";
    private const string SearchVersionKey = "products:search:version";

    private static readonly DistributedCacheEntryOptions ProductOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    private static readonly DistributedCacheEntryOptions SearchOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
    };

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var key = ProductByIdPrefix + id.ToString("N");
        var cached = await GetAsync<ProductDto>(key, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogCacheHit(key);
            return cached;
        }

        LogCacheMiss(key);
        var product = await inner.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (product is not null)
        {
            await SetAsync(key, product, ProductOptions, ct).ConfigureAwait(false);
        }

        return product;
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        var key = ProductBySkuPrefix + sku.ToLowerInvariant();
        var cached = await GetAsync<ProductDto>(key, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogCacheHit(key);
            return cached;
        }

        LogCacheMiss(key);
        var product = await inner.GetBySkuAsync(sku, ct).ConfigureAwait(false);
        if (product is not null)
        {
            await SetAsync(key, product, ProductOptions, ct).ConfigureAwait(false);
        }

        return product;
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(
        ProductSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = await GetSearchVersionAsync(ct).ConfigureAwait(false);
        var key = $"{SearchPrefix}{version}:{HashRequest(request)}";

        var cached = await GetAsync<PagedResult<ProductDto>>(key, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            LogCacheHit(key);
            return cached;
        }

        LogCacheMiss(key);
        var result = await inner.SearchAsync(request, ct).ConfigureAwait(false);
        await SetAsync(key, result, SearchOptions, ct).ConfigureAwait(false);
        return result;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = await inner.CreateAsync(request, ct).ConfigureAwait(false);
        await InvalidateSearchAsync(ct).ConfigureAwait(false);
        return product;
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await inner.UpdateAsync(id, request, ct).ConfigureAwait(false);
        await RemoveProductKeysAsync(product, ct).ConfigureAwait(false);
        await InvalidateSearchAsync(ct).ConfigureAwait(false);
        return product;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await inner.DeleteAsync(id, ct).ConfigureAwait(false);
        await cache.RemoveAsync(ProductByIdPrefix + id.ToString("N"), ct).ConfigureAwait(false);
        await InvalidateSearchAsync(ct).ConfigureAwait(false);
    }

    private async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct).ConfigureAwait(false);
            return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheFailure(ex, key);
            return null;
        }
    }

    private async Task SetAsync<T>(string key, T value, DistributedCacheEntryOptions options, CancellationToken ct)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await cache.SetAsync(key, bytes, options, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheFailure(ex, key);
        }
    }

    private async Task RemoveProductKeysAsync(ProductDto product, CancellationToken ct)
    {
        await cache.RemoveAsync(ProductByIdPrefix + product.Id.ToString("N"), ct).ConfigureAwait(false);
        await cache.RemoveAsync(ProductBySkuPrefix + product.Sku.ToLowerInvariant(), ct).ConfigureAwait(false);
    }

    private async Task<long> GetSearchVersionAsync(CancellationToken ct)
    {
        var raw = await cache.GetStringAsync(SearchVersionKey, ct).ConfigureAwait(false);
        if (raw is not null && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            return v;
        }

        var initial = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await cache.SetStringAsync(SearchVersionKey, initial.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        return initial;
    }

    private async Task InvalidateSearchAsync(CancellationToken ct)
    {
        var version = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await cache.SetStringAsync(SearchVersionKey, version.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
    }

    private static string HashRequest(ProductSearchRequest request)
    {
        var raw = string.Create(CultureInfo.InvariantCulture,
            $"{request.SearchTerm}|{request.CategoryId}|{request.MinPrice}|{request.MaxPrice}|{request.Page}|{request.PageSize}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
