namespace Library.Api.Domains.PriceAlarms.Features.Agents;

// 065: MCP okuma slice'ı — agent kullanıcının bu ürüne fiyat alarmı kurup kurmadığını sorar.
// İzole handler (konvansiyon). library.read scope.
public static class GetPriceAlarmStatusForAgent
{
    [RequiredScope(AuthorizationScopes.LibraryRead)]
    public record GetPriceAlarmStatusQuery(Guid UserId, Guid ProductId);

    public class PriceAlarmStatusResponse
    {
        public bool Exists { get; set; }
    }

    public class GetPriceAlarmStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<PriceAlarmStatusResponse>> Handle(
            GetPriceAlarmStatusQuery query, IQuerySession session, CancellationToken ct)
        {
            var exists = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == query.UserId && x.ProductId == query.ProductId)
                .AnyAsync(ct);

            return FeatureObjectResultModel<PriceAlarmStatusResponse>.Ok(
                new PriceAlarmStatusResponse { Exists = exists });
        }
    }
}
