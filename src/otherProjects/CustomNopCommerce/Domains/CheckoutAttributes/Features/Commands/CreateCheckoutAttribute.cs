namespace CustomNopCommerce.Domains.CheckoutAttributes.Features.Commands;

/// <summary>Yeni checkout özniteliği oluşturma write-slice'ı.</summary>
public static class CreateCheckoutAttribute
{
    public record CreateCheckoutAttributeCommand(
        string Name,
        string? TextPrompt,
        bool IsRequired,
        bool ShippableProductRequired,
        CheckoutAttributeControlType ControlType,
        int DisplayOrder);

    public class CreateCheckoutAttributeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateCheckoutAttributeCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateCheckoutAttributeResponse>> Handle(
            CreateCheckoutAttributeCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateCheckoutAttributeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = OrderingResourceConstants.CHECKOUT_ATTR_NAME_REQUIRED });

            var attribute = CheckoutAttribute.Create(cmd.Name, cmd.TextPrompt, cmd.IsRequired,
                cmd.ShippableProductRequired, cmd.ControlType, cmd.DisplayOrder);
            session.Store(attribute);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateCheckoutAttributeResponse>.Ok(
                new CreateCheckoutAttributeResponse { Id = attribute.Id });
        }
    }
}

public static class CreateCheckoutAttributeCommandEndpoint
{
    public static RouteGroupBuilder CreateCheckoutAttributeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateCheckoutAttribute.CreateCheckoutAttributeCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateCheckoutAttribute.CreateCheckoutAttributeResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateCheckoutAttribute");
        return group;
    }
}
