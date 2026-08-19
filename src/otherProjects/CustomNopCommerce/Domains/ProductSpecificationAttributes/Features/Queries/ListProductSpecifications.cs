namespace CustomNopCommerce.Domains.ProductSpecificationAttributes.Features.Queries;

/// <summary>Bir ürünün atanmış spesifikasyonlarını listeleyen read-slice'ı.</summary>
public static class ListProductSpecifications
{
    public record ListProductSpecificationsQuery(Guid ProductId);

    public class ProductSpecItem
    {
        public Guid Id { get; set; }
        public Guid SpecificationAttributeId { get; set; }
        public SpecificationAttributeType Type { get; set; }
        public Guid? OptionId { get; set; }
        public string? CustomValue { get; set; }
        public bool AllowFiltering { get; set; }
    }

    public class ListProductSpecificationsQueryHandler
    {
        public async Task<FeatureListResultModel<ProductSpecItem>> Handle(
            ListProductSpecificationsQuery query, IQuerySession session, CancellationToken ct)
        {
            var assignments = await session.Query<ProductSpecificationAttribute>()
                .Where(a => a.ProductId == query.ProductId && !a.IsDeleted)
                .ToListAsync(ct);

            var items = assignments
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new ProductSpecItem
                {
                    Id = a.Id,
                    SpecificationAttributeId = a.SpecificationAttributeId,
                    Type = a.Type,
                    OptionId = a.SpecificationAttributeOptionId,
                    CustomValue = a.CustomValue,
                    AllowFiltering = a.AllowFiltering,
                }).ToList();

            return FeatureListResultModel<ProductSpecItem>.Ok(items);
        }
    }
}

public static class ListProductSpecificationsQueryEndpoint
{
    public static RouteGroupBuilder ListProductSpecificationsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-product/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListProductSpecifications.ProductSpecItem>>(
                    new ListProductSpecifications.ListProductSpecificationsQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListProductSpecifications");
        return group;
    }
}
