namespace Catalog.Api.Domains.Categories.Features.Agent;

// 016: get-or-create — UpsertBrand ile aynı desen (R4); commit handler içinde (unique ihlali yakalanır).
public static class UpsertCategoryForAgent
{
    [InvalidatesCache("catalog-products")]
    public record UpsertCategoryCommand(string Name);

    public class UpsertCategoryResponse
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = default!; // "created" | "existing"
    }

    public class UpsertCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpsertCategoryResponse>> Handle(
            UpsertCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<UpsertCategoryResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.VALUE_EMPTY
                });

            var normalized = NameNormalization.Normalize(cmd.Name);
            var existing = await session.Query<Category>()
                .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
            if (existing is not null)
                return FeatureObjectResultModel<UpsertCategoryResponse>.Ok(new UpsertCategoryResponse
                {
                    Id = existing.Id,
                    Action = "existing"
                });

            var created = Category.Create(cmd.Name);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<UpsertCategoryResponse>.Error(created.Messages!);

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
                var winner = await session.Query<Category>()
                    .FirstAsync(x => x.NormalizedName == normalized, ct);
                return FeatureObjectResultModel<UpsertCategoryResponse>.Ok(new UpsertCategoryResponse
                {
                    Id = winner.Id,
                    Action = "existing"
                });
            }

            return FeatureObjectResultModel<UpsertCategoryResponse>.Ok(new UpsertCategoryResponse
            {
                Id = created.Data!.Id,
                Action = "created"
            });
        }
    }
}