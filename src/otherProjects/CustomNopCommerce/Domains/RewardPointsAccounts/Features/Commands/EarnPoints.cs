namespace CustomNopCommerce.Domains.RewardPointsAccounts.Features.Commands;

/// <summary>Müşteriye puan kazandırma write-slice'ı. Hesap yoksa otomatik açılır (müşteri başına tek hesap).</summary>
public static class EarnPoints
{
    public record EarnPointsCommand(Guid CustomerId, int Points, string Message, Guid? OrderId, DateTime? ExpiresAtUtc);

    public class EarnPointsResponse
    {
        public Guid AccountId { get; set; }
        public int Balance { get; set; }
    }

    [Transactional]
    public class EarnPointsCommandHandler
    {
        public async Task<FeatureObjectResultModel<EarnPointsResponse>> Handle(
            EarnPointsCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var account = await session.Query<RewardPointsAccount>()
                .Where(a => a.CustomerId == cmd.CustomerId && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);
            account ??= RewardPointsAccount.Create(cmd.CustomerId);

            var result = account.Earn(cmd.Points, cmd.Message, cmd.OrderId, DateTime.UtcNow, cmd.ExpiresAtUtc);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<EarnPointsResponse>.Error(result.Messages);

            session.Store(account);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<EarnPointsResponse>.Ok(
                new EarnPointsResponse { AccountId = account.Id, Balance = account.Balance });
        }
    }
}

public static class EarnPointsCommandEndpoint
{
    public static RouteGroupBuilder EarnPointsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/earn", async ([FromBody] EarnPoints.EarnPointsCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<EarnPoints.EarnPointsResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("EarnPoints");
        return group;
    }
}
