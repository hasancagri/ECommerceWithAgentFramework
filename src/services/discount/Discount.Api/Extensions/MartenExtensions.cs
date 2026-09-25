namespace Discount.Api.Extensions;

// Discount kalıcılık kurulumu: Marten (Postgres document store) + aggregate/read-model şemaları +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddDiscountMarten(this WebApplicationBuilder builder)
    {
        var discountDb = builder.Configuration.GetConnectionString("discountDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.DiscountSchemaName;
                opts.Connection(discountDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s =>
                    {
                        s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
                    });

                // Sabit alias'lar savunma amaçlı (tr-TR dotless-ı tuzağı — 077 dersi; bu adlarda ı yok ama
                // güvenli taraf). ProductDiscount/ProductCatalogRef read-model'leri ProductId ile kimliklenir.
                opts.Schema.For<Campaign>().DocumentAlias("campaign").Index(x => x.Status);
                opts.Schema.For<ProductDiscount>().DocumentAlias("product_discount")
                    .Identity(x => x.ProductId).Index(x => x.CampaignId);
                opts.Schema.For<ProductCatalogRef>().DocumentAlias("product_catalog_ref")
                    .Identity(x => x.ProductId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
