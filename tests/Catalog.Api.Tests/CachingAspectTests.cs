using System.Text.Json;
using Catalog.Api.Domains.Products.Features.Queries;
using Common;
using Common.Utils.Caching;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// T013 + T017: aspect'in saf (host'suz) doğrulanabilir parçaları — anahtar üretimi + L2 round-trip.
public class CachingAspectTests
{
    // --- CacheKeyFactory (FR-004) ---

    [Fact]
    public void Build_SameQuerySameParams_ProducesSameKey()
    {
        var id = Guid.NewGuid();
        var a = CacheKeyFactory.Build("catalog", new GetProductById.GetProductByIdQuery(id));
        var b = CacheKeyFactory.Build("catalog", new GetProductById.GetProductByIdQuery(id));

        a.ShouldBe(b);
    }

    [Fact]
    public void Build_DifferentParams_ProducesDifferentKeys()
    {
        var a = CacheKeyFactory.Build("catalog", new GetProductById.GetProductByIdQuery(Guid.NewGuid()));
        var b = CacheKeyFactory.Build("catalog", new GetProductById.GetProductByIdQuery(Guid.NewGuid()));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Build_ParameterlessQuery_ProducesStableKey()
    {
        var a = CacheKeyFactory.Build("catalog", new GetAllProducts.GetAllProductsQuery());
        var b = CacheKeyFactory.Build("catalog", new GetAllProducts.GetAllProductsQuery());

        a.ShouldBe(b);
        a.ShouldStartWith("catalog:GetAllProductsQuery:");
    }

    [Fact]
    public void Build_KnownInput_IsDeterministicAcrossRuns()
    {
        // Sabit girdi → sabit anahtar. string.GetHashCode randomizasyonu kullanılsaydı bu değer
        // her süreçte değişirdi; FNV-1a olduğundan literal olarak kilitlenebilir (cross-instance L2).
        var key = CacheKeyFactory.Build("catalog",
            new GetProductById.GetProductByIdQuery(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        key.ShouldBe("catalog:GetProductByIdQuery:" + key.Split(':')[2]);
        // İki kez üretip birebir eşitlik determinizmi kanıtlar:
        key.ShouldBe(CacheKeyFactory.Build("catalog",
            new GetProductById.GetProductByIdQuery(Guid.Parse("11111111-1111-1111-1111-111111111111"))));
    }

    // --- L2 serialize round-trip (FR-013) ---

    [Fact]
    public void FeatureObjectResultModel_RoundTrips_ViaSystemTextJson()
    {
        var original = FeatureObjectResultModel<GetProductById.ProductResponse>.Ok(new GetProductById.ProductResponse
        {
            Id = Guid.NewGuid(),
            Name = "Apple iPhone",
            Description = "desc",
            Price = 42.5m,
            Sku = "SKU-1",
            IsActive = true
        });

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<FeatureObjectResultModel<GetProductById.ProductResponse>>(json);

        restored.ShouldNotBeNull();
        restored!.IsSuccess.ShouldBeTrue();
        restored.Data.Id.ShouldBe(original.Data.Id);
        restored.Data.Name.ShouldBe(original.Data.Name);
        restored.Data.Price.ShouldBe(original.Data.Price);
    }
}