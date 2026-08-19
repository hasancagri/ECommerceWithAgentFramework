namespace CustomNopCommerce.Domains.RewardPointsAccounts.Features.Queries;

/// <summary>Bir müşterinin puan bakiyesini + hareket sayısını getiren read-slice'ı.</summary>
public static class GetPointsBalance
{
    public record GetPointsBalanceQuery(Guid CustomerId);

    public class GetPointsBalanceResponse
    {
        public Guid CustomerId { get; set; }
        public int Balance { get; set; }
        public int EntryCount { get; set; }
    }

    public class GetPointsBalanceQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetPointsBalanceResponse>> Handle(
            GetPointsBalanceQuery query, IQuerySession session, CancellationToken ct)
        {
            var account = await session.Query<RewardPointsAccount>()
                .Where(a => a.CustomerId == query.CustomerId && !a.IsDeleted)
                .FirstOrDefaultAsync(ct);

            // Hesap yoksa bakiye 0 kabul edilir (henüz puan kazanılmamış).
            return FeatureObjectResultModel<GetPointsBalanceResponse>.Ok(new GetPointsBalanceResponse
            {
                CustomerId = query.CustomerId,
                Balance = account?.Balance ?? 0,
                EntryCount = account?.Entries.Count ?? 0,
            });
        }
    }
}

public static class GetPointsBalanceQueryEndpoint
{
    public static RouteGroupBuilder GetPointsBalanceGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/balance/{customerId:guid}", async (Guid customerId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetPointsBalance.GetPointsBalanceResponse>>(
                    new GetPointsBalance.GetPointsBalanceQuery(customerId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetPointsBalance");
        return group;
    }
}
