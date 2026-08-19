using CustomNopCommerce.Domains.Affiliates;
using CustomNopCommerce.Domains.Categories;
using CustomNopCommerce.Domains.CheckoutAttributes;
using CustomNopCommerce.Domains.Countries;
using CustomNopCommerce.Domains.Currencies;
using CustomNopCommerce.Domains.Customers;
using CustomNopCommerce.Domains.Measures;
using CustomNopCommerce.Domains.MessageTemplates;
using CustomNopCommerce.Domains.NewsLetterSubscriptions;
using CustomNopCommerce.Domains.QueuedEmails;
using CustomNopCommerce.Domains.Discounts;
using CustomNopCommerce.Domains.GdprConsents;
using CustomNopCommerce.Domains.GdprLogEntries;
using CustomNopCommerce.Domains.GiftCards;
using CustomNopCommerce.Domains.Orders;
using CustomNopCommerce.Domains.Products;
using CustomNopCommerce.Domains.ProductAttributeCombinations;
using CustomNopCommerce.Domains.ProductAttributeMappings;
using CustomNopCommerce.Domains.ProductAttributes;
using CustomNopCommerce.Domains.ProductRecommendations;
using CustomNopCommerce.Domains.ProductSpecificationAttributes;
using CustomNopCommerce.Domains.ProductReviews;
using CustomNopCommerce.Domains.ProductTags;
using CustomNopCommerce.Domains.ReviewTypes;
using CustomNopCommerce.Domains.RewardPointsAccounts;
using CustomNopCommerce.Domains.Shipments;
using CustomNopCommerce.Domains.ShippingMethods;
using CustomNopCommerce.Domains.TaxCategories;
using CustomNopCommerce.Domains.TaxRates;
using CustomNopCommerce.Domains.TierPrices;
using CustomNopCommerce.Domains.UrlRecords;
using CustomNopCommerce.Domains.Vendors;
using CustomNopCommerce.Domains.Warehouses;
using CustomNopCommerce.Domains.SpecificationAttributeGroups;
using CustomNopCommerce.Domains.SpecificationAttributes;

var builder = WebApplication.CreateBuilder(args);

// Marten = document/event store (Postgres). Ana repo idiomu: Newtonsoft + non-public setter/ctor
// (aggregate'ler private setter'larını korur). Bağlantı lazy — health endpoint DB olmadan da çalışır.
var connString = builder.Configuration.GetConnectionString("customNopDb")!;
builder.Services.AddMarten(opts =>
{
    opts.DatabaseSchemaName = "customnop";
    opts.Connection(connString);
    opts.UseNewtonsoftForSerialization(
        nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
        configure: s =>
        {
            s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
        });

    // Catalog-Core aggregate kökleri.
    opts.Schema.For<Product>();
    opts.Schema.For<Category>();
    opts.Schema.For<ProductTag>();

    // Catalog-Variants aggregate kökleri.
    opts.Schema.For<ProductAttribute>();
    opts.Schema.For<ProductAttributeMapping>();
    opts.Schema.For<ProductAttributeCombination>();

    // Catalog-Specifications aggregate kökleri.
    opts.Schema.For<SpecificationAttributeGroup>();
    opts.Schema.For<SpecificationAttribute>();
    opts.Schema.For<ProductSpecificationAttribute>();

    // Catalog-Recommendations aggregate kökü.
    opts.Schema.For<ProductRecommendation>();

    // Reviews aggregate kökleri (ayrı BC).
    opts.Schema.For<ProductReview>();
    opts.Schema.For<ReviewType>();

    // Ordering aggregate kökleri (ayrı BC).
    opts.Schema.For<Order>();
    opts.Schema.For<GiftCard>();
    opts.Schema.For<CheckoutAttribute>();

    // Customers aggregate kökü (ayrı BC — auth/rol Identity.Server'da).
    opts.Schema.For<Customer>();

    // Pricing aggregate kökleri (ayrı BC).
    opts.Schema.For<Discount>();
    opts.Schema.For<TierPrice>();

    // Shipping aggregate kökleri (ayrı BC).
    opts.Schema.For<ShippingMethod>();
    opts.Schema.For<Warehouse>();
    opts.Schema.For<Shipment>();

    // Tax aggregate kökleri (ayrı BC).
    opts.Schema.For<TaxCategory>();
    opts.Schema.For<TaxRate>();

    // Directory aggregate kökleri (referans-veri BC).
    opts.Schema.For<Country>();
    opts.Schema.For<Currency>();
    opts.Schema.For<Measure>();

    // Loyalty aggregate kökü (ayrı BC).
    opts.Schema.For<RewardPointsAccount>();

    // Messaging aggregate kökleri (ayrı BC).
    opts.Schema.For<MessageTemplate>();
    opts.Schema.For<NewsLetterSubscription>();
    opts.Schema.For<QueuedEmail>();

    // Vendors aggregate kökü (marketplace BC).
    opts.Schema.For<Vendor>();

    // Seo aggregate kökü (ayrı BC).
    opts.Schema.For<UrlRecord>();

    // Gdpr aggregate kökleri (ayrı BC).
    opts.Schema.For<GdprConsent>();
    opts.Schema.For<GdprLogEntry>();

    // Affiliates aggregate kökü (ayrı BC).
    opts.Schema.For<Affiliate>();
});

// Wolverine = süreç-içi command/query bus (IMessageBus.InvokeAsync). Faz 0: local, transport yok.
// Durable Marten outbox / RabbitMQ, gerektiren ilk modülde eklenir (henüz mesaj/saga yok).
builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddAllDependencies();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Sağlık ucu — modüller eklendikçe listeye eklenir.
app.MapGet("/", () => Results.Ok(new
{
    app = "CustomNopCommerce",
    status = "up",
    modules = new[] { "Catalog-Core", "Catalog-Variants", "Catalog-Specifications", "Catalog-Recommendations", "Reviews", "Ordering", "Customers", "Pricing", "Shipping", "Tax", "Directory", "Loyalty", "Messaging", "Vendors", "Seo", "Gdpr", "Affiliates" }
}));

// Catalog-Core modül endpoint'leri.
app.AddProductGroupEndpointExtension();
app.AddCategoryGroupEndpointExtension();
app.AddProductTagGroupEndpointExtension();

// Catalog-Variants modül endpoint'leri.
app.AddProductAttributeGroupEndpointExtension();
app.AddProductAttributeMappingGroupEndpointExtension();
app.AddProductAttributeCombinationGroupEndpointExtension();

// Catalog-Specifications modül endpoint'leri.
app.AddSpecificationAttributeGroupGroupEndpointExtension();
app.AddSpecificationAttributeGroupEndpointExtension();
app.AddProductSpecificationAttributeGroupEndpointExtension();

// Catalog-Recommendations modül endpoint'leri.
app.AddProductRecommendationGroupEndpointExtension();

// Reviews modül endpoint'leri.
app.AddProductReviewGroupEndpointExtension();
app.AddReviewTypeGroupEndpointExtension();

// Ordering modül endpoint'leri.
app.AddOrderGroupEndpointExtension();
app.AddGiftCardGroupEndpointExtension();
app.AddCheckoutAttributeGroupEndpointExtension();

// Customers modül endpoint'leri.
app.AddCustomerGroupEndpointExtension();

// Pricing modül endpoint'leri.
app.AddDiscountGroupEndpointExtension();
app.AddTierPriceGroupEndpointExtension();

// Shipping modül endpoint'leri.
app.AddShippingMethodGroupEndpointExtension();
app.AddWarehouseGroupEndpointExtension();
app.AddShipmentGroupEndpointExtension();

// Tax modül endpoint'leri.
app.AddTaxCategoryGroupEndpointExtension();
app.AddTaxRateGroupEndpointExtension();

// Directory modül endpoint'leri.
app.AddCountryGroupEndpointExtension();
app.AddCurrencyGroupEndpointExtension();
app.AddMeasureGroupEndpointExtension();

// Loyalty modül endpoint'leri.
app.AddRewardPointsAccountGroupEndpointExtension();

// Messaging modül endpoint'leri.
app.AddMessageTemplateGroupEndpointExtension();
app.AddNewsLetterSubscriptionGroupEndpointExtension();
app.AddQueuedEmailGroupEndpointExtension();

// Vendors modül endpoint'leri.
app.AddVendorGroupEndpointExtension();

// Seo modül endpoint'leri.
app.AddUrlRecordGroupEndpointExtension();

// Gdpr modül endpoint'leri.
app.AddGdprConsentGroupEndpointExtension();
app.AddGdprLogEntryGroupEndpointExtension();

// Affiliates modül endpoint'leri.
app.AddAffiliateGroupEndpointExtension();

await app.RunAsync();