
namespace Payment.Api.Domains.Payments;

public class Payment : AggregateRoot
{
    private Payment() { }

    public Guid UserId { get; private set; }
    public string OrderCode { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    public static ResultDomain<Payment> Create(Guid userId, string orderCode, decimal amount)
    {
        if (userId == Guid.Empty)
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(UserId),
                Code = "UserId cannot be empty."
            });
        }

        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(OrderCode),
                Code = "Order code cannot be empty."
            });
        }

        if (amount <= 0)
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(Amount),
                Code = "Amount must be greater than zero."
            });
        }

        return ResultDomain<Payment>.Ok(new Payment
        {
            UserId = userId,
            OrderCode = orderCode,
            Amount = amount,
            Status = PaymentStatus.Pending
        });
    }

    public void SetStatus(PaymentStatus status)
    {
        Status = status;
    }
}

public enum PaymentStatus
{
    Success = 1,
    Failed = 2,
    Pending = 3
}