namespace CustomNopCommerce.Domains.RewardPointsAccounts.Features.Commands;

/// <summary>Müşteri puanı harcama write-slice'ı. Bakiye yetersizse reddedilir (aggregate invariant'ı).</summary>
public static class RedeemPoints
{
    public record RedeemPointsCommand(Guid CustomerId, int Points, string Message, Guid? OrderId);

    public class RedeemPointsResponse
    {
        public int Balance { get; set; }
    }

    [Transactional]
    public class RedeemPointsCommandHandler
    {
        public async Task<FeatureObjectResultModel<RedeemPointsResponse>> Handle(
            RedeemPointsCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var account = await session.Query<RewardPointsAccount>()
                .Where(a => a.CustomerId == cmd.CustomerId && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (account is null)
                return FeatureObjectResultModel<RedeemPointsResponse>.Error(new MessageItem
                { Property = nameof(cmd.CustomerId), Code = LoyaltyResourceConstants.ACCOUNT_NOT_FOUND });

            var result = account.Redeem(cmd.Points, cmd.Message, cmd.OrderId, DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RedeemPointsResponse>.Error(result.Messages);

            session.Update(account);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<RedeemPointsResponse>.Ok(
                new RedeemPointsResponse { Balance = account.Balance });
        }
    }
}

public static class RedeemPointsCommandEndpoint
{
    public static RouteGroupBuilder RedeemPointsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/redeem", async ([FromBody] RedeemPoints.RedeemPointsCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RedeemPoints.RedeemPointsResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("RedeemPoints");
        return group;
    }
}
