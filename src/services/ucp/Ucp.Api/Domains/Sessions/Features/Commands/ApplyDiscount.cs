namespace Ucp.Api.Domains.Sessions.Features.Commands;

/// <summary>
/// Session'a indirim kodları uygular (discount uzantısı) — geçerli kod totals'a yansır, geçersiz kod
/// messages ile döner (session hataya düşmez). UpdateSession birleşik yapar; bu slice tekil çağrı (FR-018).
/// NOT (backlog): indirim şimdilik UCP-içi DiscountResolver; ileride ayrı Discount.Api BC'ye taşınacak
/// (kod doğrulama uzak BC'de) — kullanıcı kararı.
/// </summary>
public static class ApplyDiscount
{
    public record ApplyDiscountCommand(string SessionId, IReadOnlyList<string> Codes);

    [Transactional]
    public class ApplyDiscountHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            ApplyDiscountCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var agg = await SessionStore.LoadAsync(session, cmd.SessionId, ct);
            if (agg is null) return FeatureObjectResultModel<SessionResult>.NotFound();

            var disc = DiscountResolver.Resolve(cmd.Codes, agg.Totals.SubtotalMinor);
            var applied = agg.ApplyDiscounts(disc.Applied);
            if (!applied.IsSuccess) return FeatureObjectResultModel<SessionResult>.Error(applied.Messages);

            agg.MarkReadyIfComplete(DateTimeOffset.UtcNow);
            session.Update(agg);

            var model = FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
            if (disc.Messages.Count > 0) model.Messages = disc.Messages;
            return model;
        }
    }
}