namespace CustomNopCommerce.Domains.TaxCategories.Features.Commands;

/// <summary>Yeni vergi kategorisi oluşturma write-slice'ı.</summary>
public static class CreateTaxCategory
{
    public record CreateTaxCategoryCommand(string Name, int DisplayOrder);

    public class CreateTaxCategoryResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateTaxCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateTaxCategoryResponse>> Handle(
            CreateTaxCategoryCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateTaxCategoryResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = TaxResourceConstants.CATEGORY_NAME_REQUIRED });

            var category = TaxCategory.Create(cmd.Name, cmd.DisplayOrder);
            session.Store(category);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateTaxCategoryResponse>.Ok(
                new CreateTaxCategoryResponse { Id = category.Id });
        }
    }
}

public static class CreateTaxCategoryCommandEndpoint
{
    public static RouteGroupBuilder CreateTaxCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateTaxCategory.CreateTaxCategoryCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateTaxCategory.CreateTaxCategoryResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateTaxCategory");
        return group;
    }
}
