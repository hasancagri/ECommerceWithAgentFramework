using Catalog.Api.Domains.Products.Features.Agents.Commands;

namespace Catalog.Api.Tests;

// 083 US3 domain-TDD (İLKE VI): publish_imported yayın kapısı (saf seçim mantığı). Yalnız
// yayınlanmamış + fiyat>0 taslak yayınlanır; fiyatsız + zaten-yayında dışarıda kalır. Köken (import ∧
// import-dışı) ayrımı yapısal (yalnız ImportRow.ProductId'ler yüklenir) — handler entegrasyonunda doğrulanır.
public class PublishImportedTests
{
    private static Product Draft(decimal price) =>
        Product.Create("Kitap", "ISBN-1", ProductType.Simple, Money.Create(price)!, "", "tam");

    [Fact]
    public void IsPublishable_DraftWithPrice_True()
    {
        PublishImported.IsPublishable(Draft(120m)).ShouldBeTrue();
    }

    [Fact]
    public void IsPublishable_DraftWithoutPrice_False()
    {
        PublishImported.IsPublishable(Draft(0m)).ShouldBeFalse();
    }

    [Fact]
    public void IsPublishable_AlreadyPublished_False()
    {
        var product = Draft(120m);
        product.Publish().IsSuccess.ShouldBeTrue();

        PublishImported.IsPublishable(product).ShouldBeFalse();
    }
}
