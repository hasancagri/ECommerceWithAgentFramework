namespace Catalog.Api.Domains.Categories.Features.Commands;

public static class UpdateCategory
{
    public record UpdateCategoryCommand(Guid Id, string Name, Guid? ParentCategoryId, int DisplayOrder,
        bool Published, string? MetaTitle = null, string? MetaKeywords = null, string? MetaDescription = null);

    public class UpdateCategoryResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class UpdateCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateCategoryResponse>> Handle(
            UpdateCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var category = await session.LoadAsync<Category>(cmd.Id, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<UpdateCategoryResponse>.NotFound();

            var rename = category.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureObjectResultModel<UpdateCategoryResponse>.Error(rename.Messages);

            // Tam ağaç döngü kontrolü handler'da (Category.SetParent yalnız öz-ebeveyni engeller):
            // yeni parent'tan köke yürü; zincirde bu kategori görülürse torun-ebeveyn döngüsü vardır.
            if (cmd.ParentCategoryId is not null && cmd.ParentCategoryId != category.ParentCategoryId)
            {
                var cursor = cmd.ParentCategoryId;
                while (cursor is not null)
                {
                    if (cursor == category.Id)
                        return FeatureObjectResultModel<UpdateCategoryResponse>.Error(new MessageItem
                        {
                            Property = nameof(cmd.ParentCategoryId),
                            Code = CatalogResourceConstants.CATEGORY_PARENT_CYCLE
                        });

                    var ancestor = await session.LoadAsync<Category>(cursor.Value, ct);
                    if (ancestor is null || ancestor.IsDeleted)
                        return FeatureObjectResultModel<UpdateCategoryResponse>.Error(new MessageItem
                        {
                            Property = nameof(cmd.ParentCategoryId),
                            Code = CatalogResourceConstants.RECORD_NOT_FOUND
                        });
                    cursor = ancestor.ParentCategoryId;
                }
            }

            var parent = category.SetParent(cmd.ParentCategoryId);
            if (!parent.IsSuccess)
                return FeatureObjectResultModel<UpdateCategoryResponse>.Error(parent.Messages);

            var reorder = category.Reorder(cmd.DisplayOrder);
            if (!reorder.IsSuccess)
                return FeatureObjectResultModel<UpdateCategoryResponse>.Error(reorder.Messages);

            var publish = category.SetPublished(cmd.Published);
            if (!publish.IsSuccess)
                return FeatureObjectResultModel<UpdateCategoryResponse>.Error(publish.Messages);

            var seo = category.SetSeo(SeoMetadata.Create(cmd.MetaTitle, cmd.MetaKeywords, cmd.MetaDescription));
            if (!seo.IsSuccess)
                return FeatureObjectResultModel<UpdateCategoryResponse>.Error(seo.Messages);

            session.Store(category);
            return FeatureObjectResultModel<UpdateCategoryResponse>.Ok(
                new UpdateCategoryResponse { Id = category.Id });
        }
    }
}

public static class UpdateCategoryCommandEndpoint
{
    public static RouteGroupBuilder UpdateCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/", async ([FromBody] UpdateCategory.UpdateCategoryCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateCategory.UpdateCategoryResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("UpdateCategory");
        return group;
    }
}