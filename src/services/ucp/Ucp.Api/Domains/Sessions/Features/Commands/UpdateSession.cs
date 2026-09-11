namespace Ucp.Api.Domains.Sessions.Features.Commands;

/// <summary>
/// Session'ı günceller (POST /ucp/checkout_sessions/{id}) — kalem listesi TAM değişim + alıcı + kargo
/// seçimi + indirim kodları (hepsi opsiyonel; verilen uygulanır). Her değişimde totals yeniden hesaplanır;
/// sonra ready koşulları değerlendirilir (hazırsa ready_for_complete, değilse incomplete + eksik messages).
/// Kargo/indirim ayrı slice'larda da var (SelectFulfillment/ApplyDiscount); bu tek uç hepsini birleştirir.
/// </summary>
public static class UpdateSession
{
    public record LineItemInput(string ProductId, int Quantity);
    public record BuyerInput(string Email, string FirstName, string LastName);
    public record FulfillmentInput(string SelectedOptionId, string? DestinationId);
    public record UpdateSessionCommand(
        string SessionId,
        IReadOnlyList<LineItemInput>? LineItems,
        BuyerInput? Buyer,
        FulfillmentInput? Fulfillment,
        IReadOnlyList<string>? DiscountCodes);

    [Transactional]
    public class UpdateSessionHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            UpdateSessionCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var agg = await SessionStore.LoadAsync(session, cmd.SessionId, ct);
            if (agg is null) return FeatureObjectResultModel<SessionResult>.NotFound();

            var messages = new List<MessageItem>();

            // 1) Kalem TAM değişim (verilirse). Fiyat/başlık katalogdan çözülür.
            if (cmd.LineItems is not null)
            {
                var requested = cmd.LineItems.Select(l => new LineItemResolver.RequestedLine(l.ProductId, l.Quantity)).ToList();
                var resolved = await LineItemResolver.ResolveAsync(requested, session, ct);
                messages.AddRange(resolved.Messages);
                if (resolved.Items.Count > 0)
                {
                    var replaced = agg.ReplaceLineItems(resolved.Items);
                    if (!replaced.IsSuccess) messages.AddRange(replaced.Messages ?? []);
                }
            }

            // 2) Alıcı.
            if (cmd.Buyer is not null)
            {
                var buyer = UcpBuyer.Create(cmd.Buyer.Email, cmd.Buyer.FirstName, cmd.Buyer.LastName);
                if (buyer.IsSuccess) agg.SetBuyer(buyer.Data!);
                else messages.AddRange(buyer.Messages ?? []);
            }

            // 3) İndirim kodları (totals subtotal'a göre; kalem değişiminden SONRA).
            if (cmd.DiscountCodes is not null)
            {
                var disc = DiscountResolver.Resolve(cmd.DiscountCodes, agg.Totals.SubtotalMinor);
                messages.AddRange(disc.Messages);
                agg.ApplyDiscounts(disc.Applied);
            }

            // 4) Kargo seçimi (subtotal'a göre bedel; indirimden bağımsız — kargo subtotal eşiği).
            if (cmd.Fulfillment is not null)
            {
                var ful = FulfillmentResolver.Resolve(
                    cmd.Fulfillment.SelectedOptionId, cmd.Fulfillment.DestinationId ?? "", agg.Totals.SubtotalMinor);
                if (ful.IsSuccess) agg.SelectFulfillment(ful.Data!);
                else messages.AddRange(ful.Messages ?? []);
            }

            // 5) Ready değerlendir (eksikler messages'a eklenir; session hataya düşmez).
            var ready = agg.MarkReadyIfComplete(DateTimeOffset.UtcNow);
            if (!ready.IsSuccess) messages.AddRange(ready.Messages ?? []);

            session.Update(agg);

            var model = FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
            if (messages.Count > 0) model.Messages = messages;
            return model;
        }
    }
}