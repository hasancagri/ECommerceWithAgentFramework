namespace Catalog.Api.Domains.Categories.Features.Commands;

public static class CreateCategory
{
    public record CreateCategoryCommand(string Name, string Description = "",
        Guid? ParentCategoryId = null, int DisplayOrder = 0);

    public class CreateCategoryResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateCategoryResponse>> Handle(
            CreateCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // REST yaratımı bilinçlidir: aynı ad varsa get-or-create DEĞİL, açık hata (feed yolu Upsert'te).
            var normalized = NameNormalization.Normalize(cmd.Name ?? string.Empty);
            var exists = await session.Query<Category>()
                .AnyAsync(x => x.NormalizedName == normalized && !x.IsDeleted, ct);
            if (exists)
                return FeatureObjectResultModel<CreateCategoryResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.CATEGORY_ALREADY_EXISTS
                });

            if (cmd.ParentCategoryId is not null)
            {
                var parent = await session.LoadAsync<Category>(cmd.ParentCategoryId.Value, ct);
                if (parent is null || parent.IsDeleted)
                    return FeatureObjectResultModel<CreateCategoryResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.ParentCategoryId),
                        Code = CatalogResourceConstants.RECORD_NOT_FOUND
                    });
            }

            var created = Category.Create(cmd.Name!, cmd.Description, cmd.ParentCategoryId, cmd.DisplayOrder);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreateCategoryResponse>.Error(created.Messages);

            var category = created.Data!;

            // K8 düzeni: yazılan kategori vitrindedir.
            var publish = category.SetPublished(true);
            if (!publish.IsSuccess)
                return FeatureObjectResultModel<CreateCategoryResponse>.Error(publish.Messages);

            session.Store(category);
            return FeatureObjectResultModel<CreateCategoryResponse>.Ok(
                new CreateCategoryResponse { Id = category.Id });
        }
    }
}

public static class CreateCategoryCommandEndpoint
{
    public static RouteGroupBuilder CreateCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateCategory.CreateCategoryCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateCategory.CreateCategoryResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateCategory");
        return group;
    }
}