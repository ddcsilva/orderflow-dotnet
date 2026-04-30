using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrderFlow.Catalog.Application.DTOs;
using OrderFlow.Catalog.Application.Interfaces;
using OrderFlow.Catalog.Infrastructure.Caching;

namespace OrderFlow.Catalog.Infrastructure.Tests.Caching;

public class CachedProductServiceTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static ProductDto SampleProduct(Guid? id = null) => new(
        Id: id ?? Guid.NewGuid(),
        Name: "Caneta",
        Description: "Azul",
        Sku: "SKU-001",
        Price: 9.90m,
        StockQuantity: 100,
        IsActive: true,
        CategoryId: Guid.NewGuid(),
        CategoryName: "Papelaria",
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: null);

    [Fact]
    public async Task GetByIdAsync_CacheMiss_DelegatesToInner_AndCachesResult()
    {
        var product = SampleProduct();
        var inner = new Mock<IProductService>();
        inner.Setup(s => s.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(product);

        var sut = new CachedProductService(inner.Object, CreateCache(), NullLogger<CachedProductService>.Instance);

        var first = await sut.GetByIdAsync(product.Id);
        var second = await sut.GetByIdAsync(product.Id);

        first.Should().BeEquivalentTo(product);
        second.Should().BeEquivalentTo(product);
        inner.Verify(s => s.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBySkuAsync_CacheHit_OnSecondCall()
    {
        var product = SampleProduct();
        var inner = new Mock<IProductService>();
        inner.Setup(s => s.GetBySkuAsync(product.Sku, It.IsAny<CancellationToken>()))
             .ReturnsAsync(product);

        var sut = new CachedProductService(inner.Object, CreateCache(), NullLogger<CachedProductService>.Instance);

        await sut.GetBySkuAsync(product.Sku);
        await sut.GetBySkuAsync(product.Sku);

        inner.Verify(s => s.GetBySkuAsync(product.Sku, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_CachesResults_PerRequestSignature()
    {
        var pageOne = new PagedResult<ProductDto>(new[] { SampleProduct() }, 1, 1, 10);
        var pageTwo = new PagedResult<ProductDto>(Array.Empty<ProductDto>(), 0, 2, 10);

        var inner = new Mock<IProductService>();
        inner.Setup(s => s.SearchAsync(It.Is<ProductSearchRequest>(r => r.Page == 1), It.IsAny<CancellationToken>()))
             .ReturnsAsync(pageOne);
        inner.Setup(s => s.SearchAsync(It.Is<ProductSearchRequest>(r => r.Page == 2), It.IsAny<CancellationToken>()))
             .ReturnsAsync(pageTwo);

        var sut = new CachedProductService(inner.Object, CreateCache(), NullLogger<CachedProductService>.Instance);

        var first1 = await sut.SearchAsync(new ProductSearchRequest(Page: 1));
        var first2 = await sut.SearchAsync(new ProductSearchRequest(Page: 1));
        var page2 = await sut.SearchAsync(new ProductSearchRequest(Page: 2));

        first1.TotalCount.Should().Be(1);
        first2.TotalCount.Should().Be(1);
        page2.TotalCount.Should().Be(0);
        inner.Verify(s => s.SearchAsync(It.Is<ProductSearchRequest>(r => r.Page == 1), It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(s => s.SearchAsync(It.Is<ProductSearchRequest>(r => r.Page == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_RemovesById_AndInvalidatesSearchCache()
    {
        var product = SampleProduct();
        var inner = new Mock<IProductService>();
        inner.Setup(s => s.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(product);
        var search1 = new PagedResult<ProductDto>(new[] { product }, 1, 1, 10);
        var search2 = new PagedResult<ProductDto>(Array.Empty<ProductDto>(), 0, 1, 10);
        inner.SetupSequence(s => s.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(search1)
             .ReturnsAsync(search2);

        var sut = new CachedProductService(inner.Object, CreateCache(), NullLogger<CachedProductService>.Instance);

        await sut.GetByIdAsync(product.Id);
        await sut.SearchAsync(new ProductSearchRequest());

        await sut.DeleteAsync(product.Id);

        await sut.GetByIdAsync(product.Id);
        var afterInvalidation = await sut.SearchAsync(new ProductSearchRequest());

        afterInvalidation.TotalCount.Should().Be(0);
        inner.Verify(s => s.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
        inner.Verify(s => s.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        inner.Verify(s => s.DeleteAsync(product.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
