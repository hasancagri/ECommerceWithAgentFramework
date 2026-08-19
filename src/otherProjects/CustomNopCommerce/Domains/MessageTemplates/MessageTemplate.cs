namespace CustomNopCommerce.Domains.MessageTemplates;

/// <summary>
/// Mesaj şablonu (ör. "Sipariş Alındı", "Kargo Yolda") — Messaging bounded context'inin aggregate kökü.
/// Konu + gövde token'lı metin taşır; gerçek gönderim token'ları doldurup QueuedEmail üretir. IsActive için
/// AggregateRoot.IsActive yeniden kullanılır. nopCommerce MessageTemplate paritesi (EmailAccount/delay/download
/// çıkarıldı — SMTP config Options'a, gecikme kuyruk zamanlamasına aittir).
/// </summary>
public class MessageTemplate : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = string.Empty;
    public string? BccEmailAddresses { get; private set; }

    private MessageTemplate() { }

    /// <summary>Yeni şablon oluşturur (aktif doğar). Ad/konu guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateMessageTemplateCommandHandler</remarks>
    public static MessageTemplate Create(string name, string subject, string body, string? bccEmailAddresses) =>
        new() { Name = name, Subject = subject, Body = body, BccEmailAddresses = bccEmailAddresses };

    /// <summary>Şablonun konu + gövdesini günceller.</summary>
    /// <remarks>Handler: UpdateMessageTemplateBodyCommandHandler</remarks>
    public ResultDomain UpdateContent(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(subject), Code = MessagingResourceConstants.TEMPLATE_SUBJECT_REQUIRED });
        Subject = subject;
        Body = body;
        return ResultDomain.Ok();
    }

    /// <summary>Şablonu aktifleştirir/pasifleştirir.</summary>
    /// <remarks>Handler: (ileride ToggleMessageTemplate)</remarks>
    public ResultDomain SetActive(bool active)
    {
        IsActive = active;
        return ResultDomain.Ok();
    }
}
