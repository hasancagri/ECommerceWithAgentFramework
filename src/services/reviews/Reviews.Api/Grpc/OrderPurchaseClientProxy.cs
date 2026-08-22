namespace Reviews.Api.Grpc;

// 044 FR-008: Order satin-alma kaniti istemcisi. null = kanal erisilemez (fail-closed karari
// handler'da REVIEW_PURCHASE_CHECK_UNAVAILABLE'a cevrilir). Retry YOK — kullanici tekrar dener.
public class OrderPurchaseClientProxy(OrderPurchase.OrderPurchaseClient client)
{
    // SubmitReview < 1sn hedefi (plan): kisa deadline — asili kalinmaz.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    public async Task<bool?> HasConfirmedPurchaseAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        try
        {
            var reply = await client.HasConfirmedPurchaseAsync(
                new HasConfirmedPurchaseRequest
                {
                    UserId = userId.ToString(),
                    ProductId = productId.ToString()
                },
                deadline: DateTime.UtcNow.Add(Deadline),
                cancellationToken: ct);

            return reply.HasPurchase;
        }
        catch (RpcException)
        {
            // Unavailable/DeadlineExceeded/PermissionDenied dahil: kanit YOK sayilmaz, kanal YOK sayilir.
            return null;
        }
    }
}
