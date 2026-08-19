namespace CustomNopCommerce.Domains.ReviewTypes.Features.Commands;

/// <summary>Yeni yorum kriteri (Kalite/Fiyat...) oluşturma write-slice'ı.</summary>
public static class CreateReviewType
{
    public record CreateReviewTypeCommand(
        string Name,
        string Description,
        int DisplayOrder,
        bool VisibleToAllCustomers,
        bool IsRequired);

    public class CreateReviewTypeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateReviewTypeCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateReviewTypeResponse>> Handle(
            CreateReviewTypeCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateReviewTypeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.REVIEWTYPE_NAME_REQUIRED });

            var reviewType = ReviewType.Create(cmd.Name, cmd.Description, cmd.DisplayOrder,
                cmd.VisibleToAllCustomers, cmd.IsRequired);
            session.Store(reviewType);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateReviewTypeResponse>.Ok(
                new CreateReviewTypeResponse { Id = reviewType.Id });
        }
    }
}

public static class CreateReviewTypeCommandEndpoint
{
    public static RouteGroupBuilder CreateReviewTypeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateReviewType.CreateReviewTypeCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateReviewType.CreateReviewTypeResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateReviewType");
        return group;
    }
}
