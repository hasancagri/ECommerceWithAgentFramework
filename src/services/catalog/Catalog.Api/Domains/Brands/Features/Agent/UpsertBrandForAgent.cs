namespace Catalog.Api.Domains.Brands.Features.Agent;

// 016: get-or-create — normalize adla sorgula, yoksa oluştur (R4). Deterministik koddur; LLM karar vermez.
// [Transactional] YOK: unique ihlalini yakalayıp mevcut kaydı okuyabilmek için commit handler içindedir.
public static class UpsertBrandForAgent
{
    [InvalidatesCache("catalog-products")]
    public record UpsertBrandCommand(string Name);

    public class UpsertBrandResponse
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = default!; // "created" | "existing"
    }

    public class UpsertBrandCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpsertBrandResponse>> Handle(
            UpsertBrandCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<UpsertBrandResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.VALUE_EMPTY
                });

            var normalized = NameNormalization.Normalize(cmd.Name);
            var existing = await session.Query<Brand>()
                .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
            if (existing is not null)
                return FeatureObjectResultModel<UpsertBrandResponse>.Ok(new UpsertBrandResponse
                {
                    Id = existing.Id,
                    Action = "existing"
                });

            var created = Brand.Create(cmd.Name);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<UpsertBrandResponse>.Error(created.Messages!);

            session.Store(created.Data!);
            try
            {
                await session.SaveChangesAsync(ct);
            }
            catch (Exception ex) when ((ex.InnerException ?? ex) is Npgsql.PostgresException
                                       {
                                           SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
                                       })
            {
                // Yakın yarış: başka yazım aynı NormalizedName'i kazandı — bir kez yeniden oku (R4).
                var winner = await session.Query<Brand>()
                    .FirstAsync(x => x.NormalizedName == normalized, ct);
                return FeatureObjectResultModel<UpsertBrandResponse>.Ok(new UpsertBrandResponse
                {
                    Id = winner.Id,
                    Action = "existing"
                });
            }

            return FeatureObjectResultModel<UpsertBrandResponse>.Ok(new UpsertBrandResponse
            {
                Id = created.Data!.Id,
                Action = "created"
            });
        }
    }
}