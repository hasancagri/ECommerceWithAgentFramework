namespace Customer.Api.Domains.MerchantInformations.Features.Commands;

/// <summary>
/// DropShop merchant kimliğini (merchantId + MerchantKey) upsert eder — tekil kayıt. Operatör
/// gateway'de onboard edip dönen key'i buraya yazar; vault tokenizer bunu okur. Dev doldurma yolu.
/// </summary>
public static class SetMerchantInformation
{
    public record SetMerchantInformationCommand(Guid MerchantId, string MerchantKey);

    public class SetMerchantInformationResponse
    {
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class SetMerchantInformationCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetMerchantInformationResponse>> Handle(
            SetMerchantInformationCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var existing = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);

            // Aynı merchant → key güncelle. Kayıt yok ya da farklı merchant (re-onboard) → yeni oluştur.
            if (existing is not null && existing.MerchantId == cmd.MerchantId)
            {
                var updated = existing.UpdateKey(cmd.MerchantKey);
                if (!updated.IsSuccess)
                    return FeatureObjectResultModel<SetMerchantInformationResponse>.Error(updated.Messages);
                session.Update(existing);
                return FeatureObjectResultModel<SetMerchantInformationResponse>.Ok(
                    new SetMerchantInformationResponse { MerchantId = existing.MerchantId });
            }

            var created = MerchantInformation.Create(cmd.MerchantId, cmd.MerchantKey);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SetMerchantInformationResponse>.Error(created.Messages);

            if (existing is not null)
                session.Delete(existing);
            session.Store(created.Data!);

            return FeatureObjectResultModel<SetMerchantInformationResponse>.Ok(
                new SetMerchantInformationResponse { MerchantId = created.Data!.MerchantId });
        }
    }
}

public static class SetMerchantInformationCommandEndpoint
{
    public record SetMerchantInformationBody(Guid MerchantId, string MerchantKey);

    public static RouteGroupBuilder SetMerchantInformationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("", async ([FromBody] SetMerchantInformationBody body, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetMerchantInformation.SetMerchantInformationResponse>>(
                    new SetMerchantInformation.SetMerchantInformationCommand(body.MerchantId, body.MerchantKey));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SetMerchantInformation")
            // Admin-only capability: merchant.credentials.write yalnız admin demetinde (customer HARİÇ).
            .RequireAuthorization(AuthorizationScopes.MerchantCredentialsWrite);
        return group;
    }
}
