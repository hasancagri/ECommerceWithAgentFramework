namespace Discount.Api.Domains.Discounts.Features.Commands;

public static class CreateDiscount
{
    public record CreateDiscountCommand(Guid UserId, string Code, decimal Rate);
    
    public class CreateDiscountResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateDiscountCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateDiscountResponse>> Handle(
            CreateDiscountCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var exists = await session.Query<Discount>()
                .AnyAsync(x => x.UserId == cmd.UserId && x.Code.Value == cmd.Code, ct);

            if (exists)
                return FeatureObjectResultModel<CreateDiscountResponse>.Error(
                    new MessageItem { Property = nameof(cmd.Code), Code = "Discount code already exists for this user." });

            var result = Discount.Create(cmd.UserId, cmd.Code, cmd.Rate);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CreateDiscountResponse>.Error(result.Messages);

            session.Store(result.Data!);
            return FeatureObjectResultModel<CreateDiscountResponse>.Ok(new CreateDiscountResponse
            {
                Id = result.Data!.Id
            });
        }
    }
}

public static class CreateDiscountCommandEndpoint
{
    public static RouteGroupBuilder CreateDiscountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateDiscount.CreateDiscountCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateDiscount.CreateDiscountResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
            .WithName("CreateDiscount")
            .MapToApiVersion(1, 0)
            .Produces<CreateDiscount.CreateDiscountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationScopes.DiscountWrite);

        return group;
    }
}