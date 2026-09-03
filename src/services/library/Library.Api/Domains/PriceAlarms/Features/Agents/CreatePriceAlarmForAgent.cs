namespace Library.Api.Domains.PriceAlarms.Features.Agents;

// 065: MCP yazma slice'ı — agent chat'ten fiyat alarmı kurar. İzole handler (CreatePriceAlarm
// paritesi). Email token claim snapshot'ı (MCP tool doldurur; worker mail için kullanır, kimseye
// sormaz — R3). Aynı kullanıcı+ürüne tek alarm (idempotent). library.write scope.
public static class CreatePriceAlarmForAgent
{
    [RequiredScope(AuthorizationScopes.LibraryWrite)]
    public record CreatePriceAlarmCommand(
        Guid UserId, string Email, Guid ProductId, string ProductName, decimal CurrentPrice);

    public class CreatePriceAlarmResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class CreatePriceAlarmCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreatePriceAlarmResponse>> Handle(
            CreatePriceAlarmCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var exists = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == cmd.UserId && x.ProductId == cmd.ProductId)
                .AnyAsync(ct);
            if (exists)
                return FeatureObjectResultModel<CreatePriceAlarmResponse>.Ok(
                    new CreatePriceAlarmResponse { Message = "Bu ürün için zaten fiyat alarmın var." });

            var created = PriceAlarm.Create(cmd.UserId, cmd.Email, cmd.ProductId, cmd.ProductName, cmd.CurrentPrice);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreatePriceAlarmResponse>.Error(created.Messages);

            session.Store(created.Data!);
            return FeatureObjectResultModel<CreatePriceAlarmResponse>.Ok(
                new CreatePriceAlarmResponse { Message = "Fiyat alarmı kuruldu; fiyat düşünce mail ile haber vereceğiz." });
        }
    }
}
