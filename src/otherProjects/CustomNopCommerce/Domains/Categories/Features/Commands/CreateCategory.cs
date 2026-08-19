namespace CustomNopCommerce.Domains.Categories.Features.Commands;

/// <summary>Yeni kategori oluşturma write-slice'ı.</summary>
public static class CreateCategory
{
    public record CreateCategoryCommand(string Name, string Description, Guid? ParentCategoryId, int DisplayOrder);

    public class CreateCategoryResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateCategoryResponse>> Handle(
            CreateCategoryCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateCategoryResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.CATEGORY_NAME_REQUIRED });

            // Üst kategori verildiyse var olmalı.
            if (cmd.ParentCategoryId is { } parentId)
            {
                var parent = await session.LoadAsync<Category>(parentId, ct);
                if (parent is null || parent.IsDeleted)
                    return FeatureObjectResultModel<CreateCategoryResponse>.Error(new MessageItem
                    { Property = nameof(cmd.ParentCategoryId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
            }

            var category = Category.Create(cmd.Name, cmd.Description, cmd.ParentCategoryId, cmd.DisplayOrder);
            session.Store(category);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateCategoryResponse>.Ok(new CreateCategoryResponse { Id = category.Id });
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
