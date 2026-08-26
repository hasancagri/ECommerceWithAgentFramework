namespace Payment.Api.Domains.Payments;

public class Payment : AggregateRoot
{
    private Payment() { }

    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    // 049: iki-fazlı ödeme durum makinesi (checkout orchestrator). Eski maket akışı (Create/SetStatus/
    // Status) geriye-uyum için durur; checkout onu KULLANMAZ, aşağıdaki iki-faz alanları/metotları sürer.
    public PaymentState State { get; private set; }
    public string? AuthorizationRef { get; private set; }
    public Guid? CheckoutId { get; private set; }

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

    /// <summary>İki-faz: ödemeyi bloke eder (Authorized). PSP hop stub — lokal olarak Authorized döner.</summary>
    public static ResultDomain<Payment> Authorize(Guid userId, decimal amount, Guid checkoutId)
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
            Status = PaymentStatus.Pending,
            State = PaymentState.Authorized,
            CheckoutId = checkoutId,
            AuthorizationRef = $"AUTH-{checkoutId:N}"
        });
    }

    /// <summary>İki-faz: bloke edilen tutarı tahsil eder (Captured). Yalnız Authorized'dan; zaten Captured no-op.</summary>
    public ResultDomain Capture()
    {
        if (State == PaymentState.Captured)
            return ResultDomain.Ok();

        if (State != PaymentState.Authorized)
            return ResultDomain.Error(new MessageItem { Property = nameof(State), Code = PaymentResourceConstants.PAYMENT_INVALID_TRANSITION });

        State = PaymentState.Captured;
        Status = PaymentStatus.Success;
        return ResultDomain.Ok();
    }

    /// <summary>İki-faz: tahsil edilmemiş blokeyi serbest bırakır (Voided). Yalnız Authorized'dan; zaten Voided no-op.</summary>
    public ResultDomain Void()
    {
        if (State == PaymentState.Voided)
            return ResultDomain.Ok();

        if (State != PaymentState.Authorized)
            return ResultDomain.Error(new MessageItem { Property = nameof(State), Code = PaymentResourceConstants.PAYMENT_INVALID_TRANSITION });

        State = PaymentState.Voided;
        Status = PaymentStatus.Failed;
        return ResultDomain.Ok();
    }
}

public enum PaymentStatus
{
    Success = 1,
    Failed = 2,
    Pending = 3
}

// 049: iki-fazlı ödeme durumu (aggregate dosyasında — enum ayrı dosya/Enumeration base yok, İlke II).
public enum PaymentState
{
    Authorized = 1,
    Captured = 2,
    Voided = 3
}