namespace CustomNopCommerce.Domains.QueuedEmails;

/// <summary>
/// Kuyruğa alınmış e-posta (outbox kaydı) — Messaging bounded context'inin aggregate kökü. Zengin aggregate
/// dersi: GÖNDERİM YAŞAM DÖNGÜSÜ + retry — kuyrukta doğar, başarısız denemeler sayılır, gönderilince damgalanır
/// (zaten gönderilmiş e-posta tekrar gönderilemez). nopCommerce QueuedEmail paritesi. NOT: gerçek sistemde bu
/// iş Wolverine/RabbitMQ outbox'ının; burada öğrenme için domain modeli olarak alındı.
/// </summary>
public class QueuedEmail : AggregateRoot
{
    public string To { get; private set; } = default!;
    public string? ToName { get; private set; }
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = string.Empty;
    public QueuedEmailPriority Priority { get; private set; }
    public DateTime? DontSendBeforeUtc { get; private set; }
    public int SentTries { get; private set; }
    public DateTime? SentOnUtc { get; private set; }

    private QueuedEmail() { }

    /// <summary>E-postayı kuyruğa alır (gönderilmedi). Alıcı guard'ı handler'da.</summary>
    /// <remarks>Handler: QueueEmailCommandHandler</remarks>
    public static QueuedEmail Create(string to, string? toName, string subject, string body,
        QueuedEmailPriority priority, DateTime? dontSendBeforeUtc)
    {
        return new QueuedEmail
        {
            To = to,
            ToName = toName,
            Subject = subject,
            Body = body,
            Priority = priority,
            DontSendBeforeUtc = dontSendBeforeUtc,
        };
    }

    /// <summary>E-postayı gönderildi işaretler. Zaten gönderilmişse reddedilir (invariant).</summary>
    /// <remarks>Handler: MarkEmailSentCommandHandler</remarks>
    public ResultDomain MarkSent(DateTime sentAtUtc)
    {
        if (SentOnUtc is not null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(SentOnUtc), Code = MessagingResourceConstants.EMAIL_ALREADY_SENT });
        SentOnUtc = sentAtUtc;
        return ResultDomain.Ok();
    }

    /// <summary>Başarısız bir gönderim denemesini kaydeder (deneme sayacını artırır).</summary>
    /// <remarks>Handler: (ileride RecordEmailFailure)</remarks>
    public ResultDomain RecordFailedAttempt()
    {
        SentTries++;
        return ResultDomain.Ok();
    }
}
