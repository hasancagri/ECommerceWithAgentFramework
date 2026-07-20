using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Storefront.Api.Bootstrap;

// research.md madde 5: ilk acilista mevcut urunler icin baslangic doldurmasi (FR-011).
// Bir kerelik, arka planda calisan is — okuma aninda senkron cagri yasagini (FR-003) ihlal etmez.
// Ayni upsert mantigi (Create) Ingestion handler'larinda kullanilan mantikla aynidir.
public class StorefrontBootstrapHostedService(
    IHttpClientFactory httpClientFactory,
    BootstrapIdentityServerSettings identitySettings,
    IDocumentStore documentStore,
    ILogger<StorefrontBootstrapHostedService> logger) : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record BootstrapProductDto(Guid Id, string Name, string? ImageUrl);
    private record BootstrapStockItemDto(Guid ProductId, int Quantity);
    private record BootstrapDiscountDto(Guid ProductId, decimal Rate);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken);

            await using var session = documentStore.LightweightSession();

            await BootstrapCatalogAsync(token, session, cancellationToken);
            await BootstrapStockAsync(token, session, cancellationToken);
            await BootstrapDiscountAsync(token, session, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Bootstrap bir kerelik, iyi-niyet dolgusudur — basarisiz olsa da servis event-tetikli
            // guncellemelerle (US2/US3) calismaya devam eder; acilisi engellemez.
            logger.LogWarning(ex, "Storefront bootstrap basarisiz oldu; event-tetikli guncellemeler devam eder.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("identity");
        var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = identitySettings.TokenEndpoint,
            ClientId = identitySettings.ClientId,
            ClientSecret = identitySettings.ClientSecret,
            Scope = "catalog.read discount.read stock.read",
        }, ct);

        if (tokenResponse.IsError)
            throw new InvalidOperationException($"Bootstrap token alinamadi: {tokenResponse.Error}");

        return tokenResponse.AccessToken!;
    }

    private HttpClient CreateAuthorizedClient(string namedHttpClient, string token)
    {
        var httpClient = httpClientFactory.CreateClient(namedHttpClient);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    private async Task BootstrapCatalogAsync(string token, IDocumentSession session, CancellationToken ct)
    {
        var client = CreateAuthorizedClient("catalog-api", token);
        var products = await client.GetFromJsonAsync<List<BootstrapProductDto>>("api/v1/products", JsonOptions, ct) ?? [];

        foreach (var product in products)
        {
            var existing = await session.LoadAsync<CatalogInfo>(product.Id, ct);
            if (existing is null)
                session.Store(CatalogInfo.Create(product.Id, product.Name, product.ImageUrl, isDeleted: false, DateTime.UtcNow));
        }
    }

    private async Task BootstrapStockAsync(string token, IDocumentSession session, CancellationToken ct)
    {
        var client = CreateAuthorizedClient("stock-api", token);
        var stocks = await client.GetFromJsonAsync<List<BootstrapStockItemDto>>("api/v1/stocks/all", JsonOptions, ct) ?? [];

        foreach (var stock in stocks)
        {
            var existing = await session.LoadAsync<StockInfo>(stock.ProductId, ct);
            if (existing is null)
                session.Store(StockInfo.Create(stock.ProductId, stock.Quantity > 0, DateTime.UtcNow));
        }
    }

    private async Task BootstrapDiscountAsync(string token, IDocumentSession session, CancellationToken ct)
    {
        var client = CreateAuthorizedClient("discount-api", token);
        var discounts = await client.GetFromJsonAsync<List<BootstrapDiscountDto>>("api/v1/discounts/all", JsonOptions, ct) ?? [];

        foreach (var discount in discounts)
        {
            var existing = await session.LoadAsync<DiscountInfo>(discount.ProductId, ct);
            if (existing is null)
                session.Store(DiscountInfo.Create(discount.ProductId, discount.Rate, DateTime.UtcNow));
        }
    }
}