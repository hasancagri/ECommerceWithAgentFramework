namespace Customer.Api.Domains.MerchantInformations.Features.Queries;

/// <summary>
/// 033: admin ekranının durum görünümü — kayıtlı merchant kimliğinin varlığı. MerchantKey HİÇBİR
/// koşulda dönmez (yalnız MerchantId + son güncelleme zamanı); kayıt yoksa NotFound.
/// </summary>
public static class GetMerchantInformation
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record GetMerchantInformationQuery;

    public class MerchantInformationView
    {
        public Guid MerchantId { get; set; }
        public DateTime? UpdatedTime { get; set; }
    }

    public class GetMerchantInformationQueryHandler
    {
        public async Task<FeatureObjectResultModel<MerchantInformationView>> Handle(
            GetMerchantInformationQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var merchant = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (merchant is null)
                return FeatureObjectResultModel<MerchantInformationView>.NotFound();

            return FeatureObjectResultModel<MerchantInformationView>.Ok(new MerchantInformationView
            {
                MerchantId = merchant.MerchantId,
                UpdatedTime = merchant.UpdatedTime ?? merchant.CreatedTime
            });
        }
    }
}

public static class GetMerchantInformationQueryEndpoint
{
    public static RouteGroupBuilder GetMerchantInformationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantInformation.MerchantInformationView>>(
                    new GetMerchantInformation.GetMerchantInformationQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetMerchantInformation")
            .RequireAuthorization(AuthorizationScopes.MerchantCredentialsWrite);
        return group;
    }
}
