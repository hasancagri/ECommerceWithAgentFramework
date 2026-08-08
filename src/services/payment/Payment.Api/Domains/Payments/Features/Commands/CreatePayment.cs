namespace Payment.Api.Domains.Payments.Features.Commands;

public static class CreatePayment
{
    public record CreatePaymentCommand(
        Guid UserId,
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
            var result = Payment.Create(cmd.UserId, cmd.Amount);
            if (!result.IsSuccess)
            {
                return FeatureObjectResultModel<CreatePaymentResponse>.Error(result.Messages);
            }

            var setStatus = result.Data!.SetStatus(PaymentStatus.Success);
            if (!setStatus.IsSuccess)
                return FeatureObjectResultModel<CreatePaymentResponse>.Error(setStatus.Messages);
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
                async ([FromBody] CreatePayment.CreatePaymentCommand cmd, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
                {
                    var userId = currentUser.Load(httpContext.User).Id;
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreatePayment.CreatePaymentResponse>>(
                            cmd with { UserId = userId });
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
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