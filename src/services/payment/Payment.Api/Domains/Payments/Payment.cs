namespace Payment.Api.Domains.Payments;

public class Payment : AggregateRoot
{
    private Payment() { }

    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    /// <summary>Yeni bir Pending ödeme oluşturur; userId ve amount doğrulanır.</summary>
    /// <remarks>Handler: CreatePaymentCommandHandler</remarks>
    public static ResultDomain<Payment> Create(Guid userId, decimal amount)
    {
        var messages = new List<MessageItem>();

        if (userId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(UserId), Code = PaymentResourceConstants.PAYMENT_USER_ID_REQUIRED });

        if (amount <= 0)
            messages.Add(new MessageItem { Property = nameof(Amount), Code = PaymentResourceConstants.PAYMENT_AMOUNT_INVALID });

        if (messages.Count > 0)
            return ResultDomain<Payment>.Error(messages);

        return ResultDomain<Payment>.Ok(new Payment
        {
            UserId = userId,
            Amount = amount,
            Status = PaymentStatus.Pending
        });
    }

    /// <summary>Ödemenin durumunu verilen değere ayarlar.</summary>
    /// <remarks>Handler: CreatePaymentCommandHandler</remarks>
    public ResultDomain SetStatus(PaymentStatus status)
    {
        Status = status;
        return ResultDomain.Ok();
    }
}

public enum PaymentStatus
{
    Success = 1,
    Failed = 2,
    Pending = 3
}