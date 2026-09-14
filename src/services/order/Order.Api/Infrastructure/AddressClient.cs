namespace Order.Api.Infrastructure;

// 077: Order.Api → Customer.Api varsayılan adres istemcisi (S2S; makine token customer.read, SagaTokenHandler).
// start_payment siparişi kullanıcının varsayılan adresine bağlar (FR-001b). Fail-closed: adres yok/erişilemez
// → null → start_payment dostça Result hatası ("varsayılan adres bulunamadı"). Adres LLM'e girmez.
public sealed class AddressClient(HttpClient http)
{
    public sealed record DefaultAddress(string Province, string District, string Street, string ZipCode, string Line);

    private sealed record DefaultAddressDto(string Province, string District, string Street, string ZipCode, string Line);

    public async Task<DefaultAddress?> GetDefaultAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"api/v1/internal/addresses/default?userId={userId}", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<DefaultAddressDto>(cancellationToken: ct);
            return dto is null ? null : new DefaultAddress(dto.Province, dto.District, dto.Street, dto.ZipCode, dto.Line);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }
}
