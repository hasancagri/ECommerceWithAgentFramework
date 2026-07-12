namespace Payment.Api.Domains.Payments.Features.Queries;

public static class GetAllPaymentsByUserId
{
    public record GetAllPaymentsByUserIdQuery(Guid UserId);

    public class GetAllPaymentsByUserIdResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedTime { get; set; }
        public PaymentStatus Status { get; set; }

        public static GetAllPaymentsByUserIdResponse From(Payment payment) => new()
        {
            Id = payment.Id,
            Amount = payment.Amount,
            CreatedTime = payment.CreatedTime,
            Status = payment.Status
        };
    }

    public class GetAllPaymentsByUserIdQueryHandler
    {
        public async Task<FeatureListResultModel<GetAllPaymentsByUserIdResponse>> Handle(
            GetAllPaymentsByUserIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var payments = await session.Query<Payment>()
                .Where(x => x.UserId == query.UserId)
                .ToListAsync(ct);

            if (!payments.Any())
            {
                return FeatureListResultModel<GetAllPaymentsByUserIdResponse>.NotFound();
            }

            return FeatureListResultModel<GetAllPaymentsByUserIdResponse>.Ok(
                payments.Select(GetAllPaymentsByUserIdResponse.From).ToList());
        }
    }
}

public static class GetAllPaymentsByUserIdEndpoint
{
    public static RouteGroupBuilder GetAllPaymentsByUserIdGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/user", async ([FromQuery] Guid userId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetAllPaymentsByUserId.GetAllPaymentsByUserIdResponse>>(
                    new GetAllPaymentsByUserId.GetAllPaymentsByUserIdQuery(userId));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetAllPaymentsByUserId")
            .MapToApiVersion(1, 0)
            .Produces<List<GetAllPaymentsByUserId.GetAllPaymentsByUserIdResponse>>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationScopes.PaymentRead);

        return group;
    }
}