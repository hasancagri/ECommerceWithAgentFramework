namespace Ucp.Api.Webhooks;

/// <summary>
/// 072 US3: giden webhook teslim izi (aggregate DEĞİL; Marten document). Bir sipariş-olayının platforma
/// teslim durumu — deneme sayacı + teslim edildi mi + son hata. SC-004 gözlemlenebilirliği.
/// </summary>
public class OutboundDelivery
{
    public Guid Id { get; set; }
    public string OrderRef { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string TargetUrl { get; set; } = default!;
    public int Attempts { get; set; }
    public bool Delivered { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
