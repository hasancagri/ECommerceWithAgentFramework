namespace Payment.Api.Domains.Payments;

public class Payment : AggregateRoot
{
    private Payment() { }

    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    // 049: tek-faz checkout ödemesi. Ödeme saga'nın SON (pivot) adımı — öncesi (sipariş/stok) geri-alınabilir,
    // sonrası geri-alma YOK, dolayısıyla Authorize/Void/refund yok (söküldü). Eski maket akışı (Create/
    // SetStatus/Status) geriye-uyum için durur; checkout onu KULLANMAZ, Charge'ı kullanır.
    public Guid? CheckoutId { get; private set; }
    public string? ChargeRef { get; private set; }

    /// <summary>Yeni bir Pending ödeme oluşturur; userId ve amount doğrulanır.</summary>
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
    public ResultDomain SetStatus(PaymentStatus status)
    {
        Status = status;
        return ResultDomain.Ok();
    }

    /// <summary>Tek-faz checkout tahsilatı: tutarı çeker (Success). PSP hop stub — lokal olarak başarılı
    /// döner. Void/refund yok; idempotency (aynı checkoutId tek ödeme) handler'da çözülür.</summary>
    public static ResultDomain<Payment> Charge(Guid userId, decimal amount, Guid checkoutId)
    {
        var messages = new List<MessageItem>();

        if (userId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(UserId), Code = PaymentResourceConstants.PAYMENT_USER_ID_REQUIRED });

        if (amount <= 0)
            messages.Add(new MessageItem { Property = nameof(Amount), Code = PaymentResourceConstants.PAYMENT_AMOUNT_INVALID });

        if (checkoutId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(CheckoutId), Code = PaymentResourceConstants.PAYMENT_CHECKOUT_ID_REQUIRED });

        if (messages.Count > 0)
            return ResultDomain<Payment>.Error(messages);

        return ResultDomain<Payment>.Ok(new Payment
        {
            UserId = userId,
            Amount = amount,
            Status = PaymentStatus.Success,
            CheckoutId = checkoutId,
            ChargeRef = $"PAY-{checkoutId:N}"
        });
    }
}

public enum PaymentStatus
{
    Success = 1,
    Failed = 2,
    Pending = 3
}