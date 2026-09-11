namespace Ucp.Api.Domains.Sessions.ValueObjects;

// UCP checkout session'ının değer nesneleri. Hepsi record + private ctor + statik Create (İlke II);
// guard'lar Create'te (Result pattern). Tutarlar ISO 4217 MINOR units (long; TRY=kuruş) — decimal
// yuvarlama drift'i ve proto amount_minor ile birebir. Catalog'un zengin Product'ından ayrı sade
// görünüm (BC izolasyonu — İlke I). Marten Newtonsoft nonpublic ctor/setter ile depolar.

/// <summary>Bir ürün kaleminin kanal görünümü — ürün referansı + başlık + birim fiyat (minor) + adet.</summary>
public record UcpLineItem
{
    private UcpLineItem() { }

    private UcpLineItem(string productId, string title, long unitPriceMinor, int quantity)
    {
        ProductId = productId;
        Title = title;
        UnitPriceMinor = unitPriceMinor;
        Quantity = quantity;
    }

    public string ProductId { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public long UnitPriceMinor { get; private set; }
    public int Quantity { get; private set; }

    /// <summary>Kalem toplamı (minor) = birim fiyat × adet.</summary>
    public long LineTotalMinor => UnitPriceMinor * Quantity;

    /// <summary>Kalem üretir; ürün/başlık boş, fiyat negatif veya adet &lt; 1 ise Error.</summary>
    public static ResultDomain<UcpLineItem> Create(string productId, string title, long unitPriceMinor, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(title))
            return ResultDomain<UcpLineItem>.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });
        if (unitPriceMinor < 0 || quantity < 1)
            return ResultDomain<UcpLineItem>.Error(new MessageItem { Code = UcpResourceConstants.INVALID_VALUE });

        return ResultDomain<UcpLineItem>.Ok(new UcpLineItem(productId, title, unitPriceMinor, quantity));
    }
}

/// <summary>Session toplamları (minor). Grand = subtotal − discount + shipping; hepsi &gt;= 0.</summary>
public record UcpTotals
{
    private UcpTotals() { }

    private UcpTotals(long subtotalMinor, long discountTotalMinor, long shippingTotalMinor, long grandTotalMinor)
    {
        SubtotalMinor = subtotalMinor;
        DiscountTotalMinor = discountTotalMinor;
        ShippingTotalMinor = shippingTotalMinor;
        GrandTotalMinor = grandTotalMinor;
    }

    public long SubtotalMinor { get; private set; }
    public long DiscountTotalMinor { get; private set; }
    public long ShippingTotalMinor { get; private set; }
    public long GrandTotalMinor { get; private set; }

    /// <summary>Toplamları hesaplar; grand negatife düşerse (indirim &gt; subtotal+shipping) 0'a klemplenir.</summary>
    public static UcpTotals Compute(long subtotalMinor, long discountTotalMinor, long shippingTotalMinor)
    {
        var grand = subtotalMinor - discountTotalMinor + shippingTotalMinor;
        if (grand < 0) grand = 0;
        return new UcpTotals(subtotalMinor, discountTotalMinor, shippingTotalMinor, grand);
    }

    /// <summary>Boş session için sıfır toplam.</summary>
    public static UcpTotals Zero() => new(0, 0, 0, 0);
}

/// <summary>Alıcı bilgisi — email + ad + soyad (PII; sipariş devrinde Order'a taşınır).</summary>
public record UcpBuyer
{
    private UcpBuyer() { }

    private UcpBuyer(string email, string firstName, string lastName)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }

    public string Email { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;

    /// <summary>Alıcı üretir; email/ad/soyad boşsa Error.</summary>
    public static ResultDomain<UcpBuyer> Create(string email, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return ResultDomain<UcpBuyer>.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });

        return ResultDomain<UcpBuyer>.Ok(new UcpBuyer(email, firstName, lastName));
    }
}

/// <summary>Seçili teslimat yöntemi — tip (shipping/pickup) + seçenek + bedel (minor) + destinasyon.</summary>
public record UcpFulfillment
{
    private UcpFulfillment() { }

    private UcpFulfillment(string methodType, string selectedOptionId, string optionLabel, long costMinor, string selectedDestinationId)
    {
        MethodType = methodType;
        SelectedOptionId = selectedOptionId;
        OptionLabel = optionLabel;
        CostMinor = costMinor;
        SelectedDestinationId = selectedDestinationId;
    }

    public string MethodType { get; private set; } = default!;
    public string SelectedOptionId { get; private set; } = default!;
    public string OptionLabel { get; private set; } = default!;
    public long CostMinor { get; private set; }
    public string SelectedDestinationId { get; private set; } = default!;

    /// <summary>Teslimat seçimi üretir; seçenek boş veya bedel negatifse Error.</summary>
    public static ResultDomain<UcpFulfillment> Create(
        string methodType, string selectedOptionId, string optionLabel, long costMinor, string selectedDestinationId)
    {
        if (string.IsNullOrWhiteSpace(selectedOptionId) || string.IsNullOrWhiteSpace(methodType))
            return ResultDomain<UcpFulfillment>.Error(new MessageItem { Code = UcpResourceConstants.FULFILLMENT_OPTION_INVALID });
        if (costMinor < 0)
            return ResultDomain<UcpFulfillment>.Error(new MessageItem { Code = UcpResourceConstants.INVALID_VALUE });

        return ResultDomain<UcpFulfillment>.Ok(
            new UcpFulfillment(methodType, selectedOptionId, optionLabel ?? "", costMinor, selectedDestinationId ?? ""));
    }
}

/// <summary>İndirim dağılımı — hangi kaleme/yola ne kadar (minor) düştüğü.</summary>
public record UcpDiscountAllocation
{
    private UcpDiscountAllocation() { }
    private UcpDiscountAllocation(string path, long amountMinor) { Path = path; AmountMinor = amountMinor; }

    public string Path { get; private set; } = default!;
    public long AmountMinor { get; private set; }

    public static UcpDiscountAllocation Create(string path, long amountMinor) => new(path ?? "", amountMinor);
}

/// <summary>Uygulanan indirim — başlık + tutar (minor) + opsiyonel kod + dağılımlar.</summary>
public record UcpAppliedDiscount
{
    private UcpAppliedDiscount() { }

    private UcpAppliedDiscount(string title, long amountMinor, string? code, IReadOnlyList<UcpDiscountAllocation> allocations)
    {
        Title = title;
        AmountMinor = amountMinor;
        Code = code;
        _allocations = allocations.ToList();
    }

    public string Title { get; private set; } = default!;
    public long AmountMinor { get; private set; }
    public string? Code { get; private set; }

    private List<UcpDiscountAllocation> _allocations = [];
    public IReadOnlyList<UcpDiscountAllocation> Allocations => _allocations;

    /// <summary>İndirim üretir; başlık boş veya tutar &lt;= 0 ise Error.</summary>
    public static ResultDomain<UcpAppliedDiscount> Create(
        string title, long amountMinor, string? code, IReadOnlyList<UcpDiscountAllocation>? allocations = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ResultDomain<UcpAppliedDiscount>.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });
        if (amountMinor <= 0)
            return ResultDomain<UcpAppliedDiscount>.Error(new MessageItem { Code = UcpResourceConstants.DISCOUNT_CODE_INVALID });

        return ResultDomain<UcpAppliedDiscount>.Ok(
            new UcpAppliedDiscount(title, amountMinor, code, allocations ?? []));
    }
}

/// <summary>Yasal/keşif bağlantısı — rel + url (gizlilik/ToS; UCP'de zorunlu).</summary>
public record UcpLink
{
    private UcpLink() { }
    private UcpLink(string rel, string url) { Rel = rel; Url = url; }

    public string Rel { get; private set; } = default!;
    public string Url { get; private set; } = default!;

    public static ResultDomain<UcpLink> Create(string rel, string url)
    {
        if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(url))
            return ResultDomain<UcpLink>.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });

        return ResultDomain<UcpLink>.Ok(new UcpLink(rel, url));
    }
}