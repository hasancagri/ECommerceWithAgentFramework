
namespace Payment.Api.Domains.Payments;

public class Payment : AggregateRoot
{
    private Payment() { }

    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    public static ResultDomain<Payment> Create(Guid userId, decimal amount)
    {
        if (userId == Guid.Empty)
        {
            return ResultDomain<Payment>.Error(new MessageItem
            {
                Property = nameof(UserId),
                Code = "UserId cannot be empty."
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