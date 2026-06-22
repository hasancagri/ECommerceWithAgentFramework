
namespace Payment.Api.Domains.Payments.Features.Queries;

public static class GetPaymentStatus
{
    public record GetPaymentStatusQuery(string OrderCode);

    public class GetPaymentStatusResponse
    {
        public Guid Id { get; set; }
        public bool IsPaid { get; set; }
        public PaymentStatus Status { get; set; }

        public static GetPaymentStatusResponse From(Payment payment) => new()
        {
            Id = payment.Id,
            IsPaid = payment.Status == PaymentStatus.Success,
            Status = payment.Status
        };
    }

    public class GetPaymentStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetPaymentStatusResponse>> Handle(
            GetPaymentStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var payment = await session.Query<Payment>()
                .FirstOrDefaultAsync(x => x.Id.ToString() == query.OrderCode, ct);

            if (payment is null)
            {
                return FeatureObjectResultModel<GetPaymentStatusResponse>.NotFound();
            }

            return FeatureObjectResultModel<GetPaymentStatusResponse>.Ok(
                GetPaymentStatusResponse.From(payment));
        }
    }
}

public static class GetPaymentStatusQueryEndpoint
{
    public static RouteGroupBuilder GetPaymentStatusEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/status/{orderCode}",
                async (string orderCode, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetPaymentStatus.GetPaymentStatusResponse>>(
                        new GetPaymentStatus.GetPaymentStatusQuery(orderCode));
                    return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
                })
            .WithName("GetPaymentStatus")
            .MapToApiVersion(1, 0)
            .Produces<GetPaymentStatus.GetPaymentStatusResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationScopes.PaymentRead);

        return group;
    }
}