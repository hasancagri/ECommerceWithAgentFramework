
namespace Payment.Api.Domains.Payments.Features.Commands;

public static class CreatePayment
{
    public record CreatePaymentCommand(
        Guid UserId,
        string OrderCode,
        string CardNumber,
        string CardHolderName,
        string CardExpirationDate,
        string CardSecurityNumber,
        decimal Amount);
    
    public class CreatePaymentResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreatePaymentCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreatePaymentResponse>> Handle(
            CreatePaymentCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var exists = await session.Query<Payment>()
                .AnyAsync(x => x.UserId == cmd.UserId && x.OrderCode == cmd.OrderCode, ct);

            if (exists)
            {
                return FeatureObjectResultModel<CreatePaymentResponse>.Error(
                    new MessageItem
                    {
                        Property = nameof(cmd.OrderCode),
                        Code = "Payment for this order already exists."
                    });
            }

            var result = Payment.Create(cmd.UserId, cmd.OrderCode, cmd.Amount);
            if (!result.IsSuccess)
            {
                return FeatureObjectResultModel<CreatePaymentResponse>.Error(result.Messages);
            }

            result.Data!.SetStatus(PaymentStatus.Success);
            session.Store(result.Data!);

            return FeatureObjectResultModel<CreatePaymentResponse>.Ok(new CreatePaymentResponse
            {
                Id = result.Data!.Id
            });
        }
    }
}

public static class CreatePaymentCommandEndpoint
{
    public static RouteGroupBuilder CreatePaymentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreatePayment.CreatePaymentCommand cmd, IMessageBus bus) =>
                {
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreatePayment.CreatePaymentResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
            .WithName("CreatePayment")
            .MapToApiVersion(1, 0)
            .Produces<CreatePayment.CreatePaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationScopes.PaymentWrite);

        return group;
    }
}