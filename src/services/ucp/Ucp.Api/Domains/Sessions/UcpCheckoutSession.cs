namespace Ucp.Api.Domains.Sessions;

/// <summary>
/// UCP checkout session durum makinesi (aggregate dosyasında; ayrı Enumeration base yok — İlke).
/// incomplete → {requires_escalation | ready_for_complete} → complete_in_progress → completed | canceled.
/// </summary>
public enum UcpSessionStatus
{
    Incomplete = 1,
    RequiresEscalation = 2,
    ReadyForComplete = 3,
    CompleteInProgress = 4,
    Completed = 5,
    Canceled = 6
}

/// <summary>
/// Bir dış-kanal satın alma niyetinin durum makinesi (UCP checkout.json'a uyumlu). Zengin aggregate:
/// koleksiyonlar private, mutasyon yalnız davranış metotlarından; toplam/indirim/kargo invariant'ları
/// içeride (İlke II). Marten identity = <see cref="AggregateRoot.Id"/> (Guid); UCP-facing kimlik
/// <see cref="SessionId"/> (protocol string, indeksli). Tutarlar minor units (TRY kuruş).
/// </summary>
public class UcpCheckoutSession : AggregateRoot
{
    private UcpCheckoutSession() { }

    private List<UcpLineItem> _lineItems = [];
    public IReadOnlyList<UcpLineItem> LineItems => _lineItems;

    private List<UcpAppliedDiscount> _discounts = [];
    public IReadOnlyList<UcpAppliedDiscount> Discounts => _discounts;

    private List<UcpLink> _links = [];
    public IReadOnlyList<UcpLink> Links => _links;

    public string SessionId { get; private set; } = default!;
    public UcpSessionStatus Status { get; private set; }
    public string Currency { get; private set; } = default!;
    public UcpTotals Totals { get; private set; } = UcpTotals.Zero();
    public UcpBuyer? Buyer { get; private set; }
    public UcpFulfillment? Fulfillment { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? ContinueUrl { get; private set; }
    public string? OrderRef { get; private set; }
    public string? IdempotencyKey { get; private set; }

    /// <summary>
    /// Session fabrikası. Yalnız TRY; en az bir yasal link zorunlu; başlangıç durumu incomplete;
    /// expires_at = now + ttlHours (belirtilmezse çağıran 6 verir — UCP varsayılanı, FR-008). Kalemler
    /// opsiyonel (create'te verilebilir) ve verilirse totals hesaplanır.
    /// </summary>
    public static ResultDomain<UcpCheckoutSession> Create(
        string sessionId,
        string currency,
        IReadOnlyList<UcpLink> links,
        int ttlHours,
        DateTimeOffset now,
        IReadOnlyList<UcpLineItem>? lineItems = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return ResultDomain<UcpCheckoutSession>.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });
        if (!string.Equals(currency, "TRY", StringComparison.OrdinalIgnoreCase))
            return ResultDomain<UcpCheckoutSession>.Error(new MessageItem { Code = UcpResourceConstants.CURRENCY_UNSUPPORTED });
        if (links is null || links.Count == 0)
            return ResultDomain<UcpCheckoutSession>.Error(new MessageItem { Code = UcpResourceConstants.LINKS_REQUIRED });

        var session = new UcpCheckoutSession
        {
            SessionId = sessionId,
            Currency = "TRY",
            Status = UcpSessionStatus.Incomplete,
            ExpiresAt = now.AddHours(ttlHours <= 0 ? 6 : ttlHours),
            _links = links.ToList(),
            _lineItems = lineItems?.ToList() ?? []
        };
        session.RecomputeTotals();
        return ResultDomain<UcpCheckoutSession>.Ok(session);
    }

    /// <summary>Kalem listesini TAM değiştirir (kısmi değil); boş liste reddedilir; totals yeniden hesaplanır.</summary>
    public ResultDomain ReplaceLineItems(IReadOnlyList<UcpLineItem> items)
    {
        var terminal = EnsureMutable();
        if (!terminal.IsSuccess) return terminal;
        if (items is null || items.Count == 0)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.LINE_ITEMS_REQUIRED });

        _lineItems = items.ToList();
        RecomputeTotals();
        return ResultDomain.Ok();
    }

    /// <summary>Alıcı bilgisini set eder (email/ad/soyad); terminal durumda reddedilir.</summary>
    public ResultDomain SetBuyer(UcpBuyer buyer)
    {
        var terminal = EnsureMutable();
        if (!terminal.IsSuccess) return terminal;

        Buyer = buyer;
        return ResultDomain.Ok();
    }

    /// <summary>Seçili kargo yöntemini set eder + bedelini totals'a yansıtır; terminal durumda reddedilir.</summary>
    public ResultDomain SelectFulfillment(UcpFulfillment fulfillment)
    {
        var terminal = EnsureMutable();
        if (!terminal.IsSuccess) return terminal;

        Fulfillment = fulfillment;
        RecomputeTotals();
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Handler'ın çözdüğü GEÇERLİ indirimleri uygular + totals'a yansıtır. Kod geçerlilik/eşleme IO'su
    /// handler'da (aggregate IO yapmaz); geçersiz kod mesajları çağıranda. Boş liste = indirim temizlenir.
    /// </summary>
    public ResultDomain ApplyDiscounts(IReadOnlyList<UcpAppliedDiscount> applied)
    {
        var terminal = EnsureMutable();
        if (!terminal.IsSuccess) return terminal;

        _discounts = (applied ?? []).ToList();
        RecomputeTotals();
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Ready koşulları (kalem + alıcı + kargo + süre-dolmamış) sağlanırsa ready_for_complete'e geçirir;
    /// aksi halde incomplete kalır ve eksikleri messages ile döner (hataya DÜŞMEZ — FR-004/FR-017).
    /// </summary>
    public ResultDomain MarkReadyIfComplete(DateTimeOffset now)
    {
        if (Status is UcpSessionStatus.Completed or UcpSessionStatus.Canceled or UcpSessionStatus.CompleteInProgress)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_ALREADY_TERMINAL });

        var missing = new List<MessageItem>();
        if (_lineItems.Count == 0) missing.Add(new MessageItem { Code = UcpResourceConstants.LINE_ITEMS_REQUIRED });
        if (Buyer is null) missing.Add(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });
        if (Fulfillment is null) missing.Add(new MessageItem { Code = UcpResourceConstants.FULFILLMENT_OPTION_INVALID });
        if (now >= ExpiresAt) missing.Add(new MessageItem { Code = UcpResourceConstants.SESSION_EXPIRED });

        if (missing.Count > 0)
        {
            Status = UcpSessionStatus.Incomplete;
            return ResultDomain.Error(missing);
        }

        Status = UcpSessionStatus.ReadyForComplete;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Devir gereken hallerde requires_escalation'a geçirir — YALNIZ ContinueUrl verilirse (FR-005).
    /// </summary>
    public ResultDomain RequireEscalation(string continueUrl)
    {
        var terminal = EnsureMutable();
        if (!terminal.IsSuccess) return terminal;
        if (string.IsNullOrWhiteSpace(continueUrl))
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });

        ContinueUrl = continueUrl;
        Status = UcpSessionStatus.RequiresEscalation;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Complete'i başlatır: ready değilse reddet, süre dolduysa reddet; idempotent — aynı IdempotencyKey
    /// (ya da zaten complete_in_progress/completed) tekrarında mevcut duruma dokunmadan Ok döner (FR-009).
    /// Başarılı ilk çağrıda status = complete_in_progress + key saklanır.
    /// </summary>
    public ResultDomain BeginComplete(string idempotencyKey, DateTimeOffset now)
    {
        // Idempotent tekrar: aynı key ile zaten süreçte/tamamlanmış → mevcut sonuç (yeni sipariş yok).
        if (Status is UcpSessionStatus.CompleteInProgress or UcpSessionStatus.Completed)
        {
            if (IdempotencyKey is not null && IdempotencyKey == idempotencyKey)
                return ResultDomain.Ok();
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_ALREADY_TERMINAL });
        }
        if (Status != UcpSessionStatus.ReadyForComplete)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_NOT_READY });
        if (now >= ExpiresAt)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_EXPIRED });

        IdempotencyKey = idempotencyKey;
        Status = UcpSessionStatus.CompleteInProgress;
        return ResultDomain.Ok();
    }

    /// <summary>Sipariş devri başarılı → completed (terminal); dış-sipariş referansını saklar.</summary>
    public ResultDomain MarkCompleted(string orderRef)
    {
        if (Status == UcpSessionStatus.Completed) return ResultDomain.Ok();
        if (Status != UcpSessionStatus.CompleteInProgress)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_NOT_READY });
        if (string.IsNullOrWhiteSpace(orderRef))
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.VALUE_IS_REQUIRED });

        OrderRef = orderRef;
        Status = UcpSessionStatus.Completed;
        return ResultDomain.Ok();
    }

    /// <summary>
    /// Ödeme/sipariş devri başarısız → complete_in_progress'ten ready_for_complete'e geri sar (tekrar
    /// denenebilir; sipariş oluşmadı). Idempotency key temizlenir (yeni deneme yeni key alır).
    /// </summary>
    public ResultDomain FailComplete()
    {
        if (Status != UcpSessionStatus.CompleteInProgress)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.INVALID_OPERATION_ERROR });

        Status = UcpSessionStatus.ReadyForComplete;
        IdempotencyKey = null;
        return ResultDomain.Ok();
    }

    /// <summary>Terminal-olmayan durumdan iptal (canceled); tamamlanmış session iptal edilemez.</summary>
    public ResultDomain Cancel(string? reason)
    {
        if (Status == UcpSessionStatus.Canceled) return ResultDomain.Ok();
        if (Status is UcpSessionStatus.Completed or UcpSessionStatus.CompleteInProgress)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_ALREADY_TERMINAL });

        Status = UcpSessionStatus.Canceled;
        return ResultDomain.Ok();
    }

    // Guard helper (invariant kontrolü — helper'a çıkarma istisnası): mutasyona kapalı terminal/süreç
    // durumlarını tek yerde reddeder (aynı guard'ın kopyalanması tutarsızlık üretir).
    private ResultDomain EnsureMutable()
    {
        if (Status is UcpSessionStatus.Completed or UcpSessionStatus.Canceled or UcpSessionStatus.CompleteInProgress)
            return ResultDomain.Error(new MessageItem { Code = UcpResourceConstants.SESSION_ALREADY_TERMINAL });
        return ResultDomain.Ok();
    }

    // Toplamları kalem/indirim/kargo'dan yeniden hesaplar (her mutasyonda + complete tazelemesinde).
    private void RecomputeTotals()
    {
        var subtotal = _lineItems.Sum(i => i.LineTotalMinor);
        var discount = _discounts.Sum(d => d.AmountMinor);
        var shipping = Fulfillment?.CostMinor ?? 0;
        Totals = UcpTotals.Compute(subtotal, discount, shipping);
    }
}