using Storefront.Api.Domains.StorefrontView;
using Storefront.Api.Domains.StorefrontView.Features.Queries;
using Shouldly;
using Xunit;

namespace Storefront.Api.Tests;

// 045 US3: aile gruplama saf çekirdeği — temsilci seçimi (stok>0 DESC, Price ASC, ProductId) +
// aile-anahtarı distinct sayımı. SQL değil; filtreli küme üzerinde bellek-içi (yüzlerce ürün ölçeği).
public class VariantGroupingTests
{
    private static StorefrontView Member(string? family, decimal price, int stock, Guid? id = null)
    {
        var view = StorefrontView.Create(id ?? Guid.NewGuid());
        view.ApplyCatalog("Üye", "Açıklama", price, Guid.NewGuid(), "Marka",
            Guid.NewGuid(), "Kategori", null, false, null, family);
        view.ApplyStock(stock);
        return view;
    }

    [Fact]
    public void PickRepresentative_PrefersInStock_ThenCheapest()
    {
        var members = new[]
        {
            Member("FAM", 100m, 0),   // stoksuz — elenir
            Member("FAM", 80m, 5),    // stokta, en ucuz — KAZANIR
            Member("FAM", 60m, 0),    // stoksuz ama daha ucuz — stok önce
        };

        var rep = GetStorefrontProductList.PickRepresentative(members);

        rep.Price.ShouldBe(80m);
        rep.StockQuantity.ShouldBe(5);
    }

    [Fact]
    public void PickRepresentative_AllOutOfStock_Deterministic()
    {
        var id1 = new Guid("11111111-1111-1111-1111-111111111111");
        var id2 = new Guid("22222222-2222-2222-2222-222222222222");
        var m1 = Member("FAM", 50m, 0, id1);
        var m2 = Member("FAM", 50m, 0, id2);

        // Hepsi stoksuz + eşit fiyat → ProductId ile deterministik (sıra-bağımsız).
        GetStorefrontProductList.PickRepresentative(new[] { m1, m2 }).ProductId
            .ShouldBe(GetStorefrontProductList.PickRepresentative(new[] { m2, m1 }).ProductId);
    }

    [Fact]
    public void GroupToRepresentatives_FamilyIsOneCard_SinglesUnchanged()
    {
        var members = new[]
        {
            Member("FAM-A", 100m, 3),
            Member("FAM-A", 90m, 3),
            Member("FAM-A", 80m, 3),   // aile temsilcisi (en ucuz stokta)
            Member(null, 50m, 1),      // ailesiz 1
            Member(null, 60m, 1),      // ailesiz 2
        };

        var reps = GetStorefrontProductList.GroupToRepresentatives(members);

        reps.Count.ShouldBe(3); // 1 aile + 2 ailesiz
        var family = reps.Single(r => r.VariantCount > 1);
        family.Representative.Price.ShouldBe(80m);
        family.VariantCount.ShouldBe(3);
        reps.Count(r => r.VariantCount == 1).ShouldBe(2);
    }

    [Fact]
    public void FamilyKey_NullFamily_UsesProductId()
    {
        var id = Guid.NewGuid();
        var solo = Member(null, 10m, 1, id);

        GetStorefrontProductList.FamilyKey(solo).ShouldBe(id.ToString());
    }

    [Fact]
    public void GroupToRepresentatives_DistinctCount_FamilyCountsOnce()
    {
        var members = new[]
        {
            Member("FAM", 100m, 3),
            Member("FAM", 90m, 3),
            Member(null, 50m, 1),
        };

        // 3 üye ama 2 kart (aile 1 + ailesiz 1) — SC-003 birebirlik.
        GetStorefrontProductList.GroupToRepresentatives(members).Count.ShouldBe(2);
    }
}
