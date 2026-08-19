namespace CustomNopCommerce.Domains.CheckoutAttributes.Features.Queries;

/// <summary>Checkout özniteliklerini (değer sayısıyla) sıralı listeleyen read-slice'ı.</summary>
public static class ListCheckoutAttributes
{
    public record ListCheckoutAttributesQuery;

    public class CheckoutAttributeItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public bool IsRequired { get; set; }
        public CheckoutAttributeControlType ControlType { get; set; }
        public int ValueCount { get; set; }
    }

    public class ListCheckoutAttributesQueryHandler
    {
        public async Task<FeatureListResultModel<CheckoutAttributeItem>> Handle(
            ListCheckoutAttributesQuery query, IQuerySession session, CancellationToken ct)
        {
            var attributes = await session.Query<CheckoutAttribute>()
                .Where(a => !a.IsDeleted)
                .ToListAsync(ct);

            var items = attributes
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new CheckoutAttributeItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    IsRequired = a.IsRequired,
                    ControlType = a.ControlType,
                    ValueCount = a.Values.Count,
                }).ToList();

            return FeatureListResultModel<CheckoutAttributeItem>.Ok(items);
        }
    }
}

public static class ListCheckoutAttributesQueryEndpoint
{
    public static RouteGroupBuilder ListCheckoutAttributesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListCheckoutAttributes.CheckoutAttributeItem>>(
                    new ListCheckoutAttributes.ListCheckoutAttributesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListCheckoutAttributes");
        return group;
    }
}
