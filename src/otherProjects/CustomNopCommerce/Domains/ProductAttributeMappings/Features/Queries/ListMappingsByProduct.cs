namespace CustomNopCommerce.Domains.ProductAttributeMappings.Features.Queries;

/// <summary>Bir ürünün attribute eşlemelerini (değer sayısıyla) listeleyen read-slice'ı.</summary>
public static class ListMappingsByProduct
{
    public record ListMappingsByProductQuery(Guid ProductId);

    public class MappingItem
    {
        public Guid Id { get; set; }
        public Guid ProductAttributeId { get; set; }
        public AttributeControlType ControlType { get; set; }
        public bool IsRequired { get; set; }
        public int ValueCount { get; set; }
    }

    public class ListMappingsByProductQueryHandler
    {
        public async Task<FeatureListResultModel<MappingItem>> Handle(
            ListMappingsByProductQuery query, IQuerySession session, CancellationToken ct)
        {
            var mappings = await session.Query<ProductAttributeMapping>()
                .Where(m => m.ProductId == query.ProductId && !m.IsDeleted)
                .ToListAsync(ct);

            var items = mappings
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new MappingItem
                {
                    Id = m.Id,
                    ProductAttributeId = m.ProductAttributeId,
                    ControlType = m.ControlType,
                    IsRequired = m.IsRequired,
                    ValueCount = m.Values.Count,
                }).ToList();

            return FeatureListResultModel<MappingItem>.Ok(items);
        }
    }
}

public static class ListMappingsByProductQueryEndpoint
{
    public static RouteGroupBuilder ListMappingsByProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-product/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListMappingsByProduct.MappingItem>>(
                    new ListMappingsByProduct.ListMappingsByProductQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListMappingsByProduct");
        return group;
    }
}
