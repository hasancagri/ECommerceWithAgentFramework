namespace Ucp.Api.Domains.Sessions.Features.Commands;

/// <summary>
/// Yeni checkout session açar (POST /ucp/checkout_sessions). Kalem fiyatı/başlığı SUNUCU-otoritesi
/// (katalog projeksiyonundan çözülür); yasal linkler + expires_at (+6h) + status=incomplete mağazadan.
/// Alıcı verilirse set edilir. Satılamaz ürünler mesajla döner (session yine de açılır — kısmi).
/// </summary>
public static class CreateSession
{
    public record LineItemInput(string ProductId, int Quantity);
    public record BuyerInput(string Email, string FirstName, string LastName);
    public record CreateSessionCommand(string Currency, IReadOnlyList<LineItemInput> LineItems, BuyerInput? Buyer);

    [Transactional]
    public class CreateSessionHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            CreateSessionCommand cmd, IDocumentSession session, UcpPlatformOption platform, CancellationToken ct)
        {
            var requested = (cmd.LineItems ?? [])
                .Select(l => new LineItemResolver.RequestedLine(l.ProductId, l.Quantity)).ToList();
            var resolved = await LineItemResolver.ResolveAsync(requested, session, ct);

            var sessionId = "ucp_" + Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var created = UcpCheckoutSession.Create(
                sessionId, cmd.Currency, DefaultLinks(platform), platform.SessionTtlHours, now, resolved.Items);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SessionResult>.Error(created.Messages);

            var agg = created.Data!;
            if (cmd.Buyer is not null)
            {
                var buyer = UcpBuyer.Create(cmd.Buyer.Email, cmd.Buyer.FirstName, cmd.Buyer.LastName);
                if (buyer.IsSuccess) agg.SetBuyer(buyer.Data!);
            }

            session.Store(agg);

            var model = FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
            if (resolved.Messages.Count > 0) model.Messages = resolved.Messages;
            return model;
        }

        // Yasal linkler mağaza tarafından enjekte edilir (platform set etmez) — StoreProfileUrl origin'inden.
        private static IReadOnlyList<UcpLink> DefaultLinks(UcpPlatformOption platform)
        {
            var origin = Uri.TryCreate(platform.StoreProfileUrl, UriKind.Absolute, out var u)
                ? u.GetLeftPart(UriPartial.Authority)
                : "https://store.example";
            return
            [
                UcpLink.Create("terms_of_service", origin + "/legal/tos").Data!,
                UcpLink.Create("privacy_policy", origin + "/legal/privacy").Data!
            ];
        }
    }
}