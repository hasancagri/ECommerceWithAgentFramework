using System.ComponentModel;
using ModelContextProtocol.Server;
// MCP tool'lari REST'ten bagimsiz Agent handler'larini dispatch eder. GlobalUsings zaten
// Features.Queries'i cektiginden ayni isimli tipler cakisir; alias ile netlestiriyoruz.
using Agent = Discount.Api.Domains.Discounts.Features.Agent;

namespace Discount.Api.Domains.Discounts;

[McpServerToolType]
public static class GetDiscountMcpTool
{
    [McpServerTool(Name = "get_discount")]
    [Description("Kupon kodundan indirim bilgisini (kod, oran) getirir; sepete kupon uygulamadan once dogrulamak icin.")]
    public static Task<FeatureObjectResultModel<Agent.GetDiscountByCode.GetDiscountByCodeResponse>> GetDiscountAsync(
        [Description("Indirim kupon kodu")] string code,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetDiscountByCode.GetDiscountByCodeResponse>>(
            new Agent.GetDiscountByCode.GetDiscountByCodeQuery(code), ct);
}