namespace Customer.Api.Domains.MerchantInformations;

/// <summary>
/// ECommerce'in DropShop gateway'indeki merchant kimliği (tekil kayıt): OAuth client_credentials için
/// <c>merchantId</c> + <c>MerchantKey</c>. Vault tokenizer bu kayıttan merchant token'ı mint eder
/// (scope <c>cards.write</c>). MerchantKey aktivasyonda bir kez verilir; operatör buraya yazar
/// (<c>SetMerchantInformation</c>). WebApp'in in-memory credential store'unun kalıcı karşılığı.
/// </summary>
public class MerchantInformation : AggregateRoot
{
    private MerchantInformation()
    {
    }

    /// <summary>DropShop merchant kimliği = OAuth client_id.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>OAuth client_secret (aktivasyon key'i); yalnız connect/token'a gider.</summary>
    public string MerchantKey { get; private set; } = string.Empty;

    /// <summary>Kayıt durumu (dev: Active).</summary>
    public string Status { get; private set; } = "Active";

    /// <summary>Yeni merchant kimliği oluşturur; merchantId + key zorunlu.</summary>
    public static ResultDomain<MerchantInformation> Create(Guid merchantId, string merchantKey)
    {
        var messages = new List<MessageItem>();

        if (merchantId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(MerchantId), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        if (string.IsNullOrWhiteSpace(merchantKey))
            messages.Add(new MessageItem { Property = nameof(MerchantKey), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        if (messages.Count > 0)
            return ResultDomain<MerchantInformation>.Error(messages);

        return ResultDomain<MerchantInformation>.Ok(new MerchantInformation
        {
            MerchantId = merchantId,
            MerchantKey = merchantKey.Trim(),
            Status = "Active"
        });
    }

    /// <summary>MerchantKey'i günceller (rotate / yeniden set); boş RET.</summary>
    public ResultDomain UpdateKey(string merchantKey)
    {
        if (string.IsNullOrWhiteSpace(merchantKey))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(MerchantKey),
                Code = CustomerResourceConstants.VALUE_IS_REQUIRED
            });
        }

        MerchantKey = merchantKey.Trim();
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}
