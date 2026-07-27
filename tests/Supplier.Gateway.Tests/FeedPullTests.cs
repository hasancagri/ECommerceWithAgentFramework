namespace Supplier.Gateway.Tests;

// Feed uç durumları: erişilemez/boş/bozuk gövde sessiz geçer (FR-008); mükerrerde ilki kazanır (FR-007).
public class FeedPullTests
{
    private static SupplierFeedRecord Wire(string externalId, decimal price = 100m)
        => new(externalId, "Ürün", "Açıklama", "Apple", "Elektronik", price, 10, null, null);

    [Fact]
    public void Parse_NullOrEmptyBody_ReturnsEmpty()
    {
        SupplierFeedAdapter.Parse(null).ShouldBeEmpty();
        SupplierFeedAdapter.Parse("").ShouldBeEmpty();
        SupplierFeedAdapter.Parse("   ").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_UnreadableBody_ReturnsEmpty()
    {
        SupplierFeedAdapter.Parse("<html>bakım sayfası</html>").ShouldBeEmpty();
        SupplierFeedAdapter.Parse("{ \"bozuk\": ").ShouldBeEmpty();
    }

    [Fact]
    public void Parse_ValidFeed_ResolvesRecords()
    {
        const string body = """
            [{ "externalId": "SUP-1", "name": "Ürün", "description": "Açıklama", "brand": "Apple",
               "price": 149.90, "stockQuantity": 25, "discountCode": null, "discountPercent": 10 }]
            """;

        var records = SupplierFeedAdapter.Parse(body);

        records.Count.ShouldBe(1);
        records[0].ExternalId.ShouldBe("SUP-1");
        records[0].DiscountPercent.ShouldBe(10m);
    }

    [Fact]
    public void FirstWins_DuplicateExternalId_KeepsFirstRecord()
    {
        var records = SupplierFeedAdapter.FirstWins(
            [Wire("SUP-1", price: 100m), Wire("SUP-2"), Wire("SUP-1", price: 999m)]);

        records.Count.ShouldBe(2);
        records[0].ExternalId.ShouldBe("SUP-1");
        records[0].Price.ShouldBe(100m); // ilki kazanır
        records[1].ExternalId.ShouldBe("SUP-2");
    }

    [Fact]
    public void FirstWins_BlankExternalId_IsDropped()
    {
        var records = SupplierFeedAdapter.FirstWins([Wire(""), Wire("  "), Wire("SUP-1")]);

        records.Count.ShouldBe(1);
        records[0].ExternalId.ShouldBe("SUP-1");
    }
}