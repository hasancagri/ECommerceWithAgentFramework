namespace Ucp.Api.Domains.Sessions.Features;

/// <summary>
/// Aggregate → UCP <c>checkout.json</c> yanıt zarfı (ince map). Aggregate private ctor + private set
/// olduğundan doğrudan serialize edilmez; endpoint bu DÜZ DTO'yu döner (071 dersi: aggregate'i doğrudan
/// yanıt yapma). <c>FeatureObjectResultModel&lt;T&gt;</c> için parametresiz ctor (class, new()) şart.
/// Durum string'i UCP enum'una birebir (incomplete/ready_for_complete/...).
/// </summary>
public class SessionResult
{
    public string Ucp { get; set; } = "checkout";
    public string Id { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Currency { get; set; } = "TRY";
    public SessionTotals Totals { get; set; } = new();
    public List<SessionLine> LineItems { get; set; } = [];
    public SessionBuyer? Buyer { get; set; }
    public SessionFulfillment? Fulfillment { get; set; }
    public List<SessionDiscount> Discounts { get; set; } = [];
    public List<SessionLink> Links { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public string? ContinueUrl { get; set; }
    public string? Order { get; set; }

    public class SessionTotals
    {
        public long Subtotal { get; set; }
        public long Discount { get; set; }
        public long Shipping { get; set; }
        public long Grand { get; set; }
    }

    public class SessionLine
    {
        public string ProductId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public long UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    public class SessionBuyer
    {
        public string Email { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
    }

    public class SessionFulfillment
    {
        public string MethodType { get; set; } = default!;
        public string SelectedOptionId { get; set; } = default!;
        public string OptionLabel { get; set; } = default!;
        public long Cost { get; set; }
        public string SelectedDestinationId { get; set; } = default!;
    }

    public class SessionDiscount
    {
        public string Title { get; set; } = default!;
        public long Amount { get; set; }
        public string? Code { get; set; }
    }

    public class SessionLink
    {
        public string Rel { get; set; } = default!;
        public string Url { get; set; } = default!;
    }

    /// <summary>Aggregate'ten yanıt DTO'su üretir (enum → UCP string).</summary>
    public static SessionResult From(UcpCheckoutSession s) => new()
    {
        Id = s.SessionId,
        Status = ToUcpStatus(s.Status),
        Currency = s.Currency,
        Totals = new SessionTotals
        {
            Subtotal = s.Totals.SubtotalMinor,
            Discount = s.Totals.DiscountTotalMinor,
            Shipping = s.Totals.ShippingTotalMinor,
            Grand = s.Totals.GrandTotalMinor
        },
        LineItems = s.LineItems.Select(i => new SessionLine
        {
            ProductId = i.ProductId,
            Title = i.Title,
            UnitPrice = i.UnitPriceMinor,
            Quantity = i.Quantity
        }).ToList(),
        Buyer = s.Buyer is null ? null : new SessionBuyer
        {
            Email = s.Buyer.Email,
            FirstName = s.Buyer.FirstName,
            LastName = s.Buyer.LastName
        },
        Fulfillment = s.Fulfillment is null ? null : new SessionFulfillment
        {
            MethodType = s.Fulfillment.MethodType,
            SelectedOptionId = s.Fulfillment.SelectedOptionId,
            OptionLabel = s.Fulfillment.OptionLabel,
            Cost = s.Fulfillment.CostMinor,
            SelectedDestinationId = s.Fulfillment.SelectedDestinationId
        },
        Discounts = s.Discounts.Select(d => new SessionDiscount
        {
            Title = d.Title,
            Amount = d.AmountMinor,
            Code = d.Code
        }).ToList(),
        Links = s.Links.Select(l => new SessionLink { Rel = l.Rel, Url = l.Url }).ToList(),
        ExpiresAt = s.ExpiresAt,
        ContinueUrl = s.ContinueUrl,
        Order = s.OrderRef
    };

    private static string ToUcpStatus(UcpSessionStatus status) => status switch
    {
        UcpSessionStatus.Incomplete => "incomplete",
        UcpSessionStatus.RequiresEscalation => "requires_escalation",
        UcpSessionStatus.ReadyForComplete => "ready_for_complete",
        UcpSessionStatus.CompleteInProgress => "complete_in_progress",
        UcpSessionStatus.Completed => "completed",
        UcpSessionStatus.Canceled => "canceled",
        _ => "incomplete"
    };
}