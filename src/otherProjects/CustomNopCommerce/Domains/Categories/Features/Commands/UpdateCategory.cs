namespace CustomNopCommerce.Domains.Categories.Features.Commands;

/// <summary>Kategori güncelleme write-slice'ı (ad, sıralama, yayın durumu).</summary>
public static class UpdateCategory
{
    public record UpdateCategoryCommand(Guid Id, string Name, int DisplayOrder, bool Published);

    [Transactional]
    public class UpdateCategoryCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UpdateCategoryCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var category = await session.LoadAsync<Category>(cmd.Id, ct);
            if (category is null || category.IsDeleted)
                return FeatureResultModel.NotFound();

            var rename = category.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureResultModel.Error(rename.Messages);

            category.Reorder(cmd.DisplayOrder);
            category.SetPublished(cmd.Published);

            session.Update(category);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UpdateCategoryCommandEndpoint
{
    public static RouteGroupBuilder UpdateCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateCategory.UpdateCategoryCommand body, IMessageBus bus) =>
            {
                var cmd = body with { Id = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("UpdateCategory");
        return group;
    }
}
