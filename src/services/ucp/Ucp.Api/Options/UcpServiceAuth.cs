namespace Ucp.Api.Options;

/// <summary>
/// UCP → Order sanksiyonlu gRPC çağrısı için makine kimliği (client_credentials; scope <c>order.write</c>).
/// Dış platform kimliği (<c>ucp-platform</c>, scope <c>dev.ucp.shopping.checkout</c>) AYRIDIR — dış taraf
/// Order'a doğrudan erişemez; sipariş devri yalnız mağazanın kendi iç makine kimliğiyle. Section <c>UcpServiceAuth</c>.
/// </summary>
public class UcpServiceAuth
{
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}