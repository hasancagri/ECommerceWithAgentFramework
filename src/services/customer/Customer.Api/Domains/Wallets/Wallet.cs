namespace Customer.Api.Domains.Wallets;

// Bir kullanicinin cuzdani. UserId ile keyli (kullanici basina tek cuzdan).
// SavedCard koleksiyonu + "en fazla 1 varsayilan" invariant'i aggregate icinde korunur.
public class Wallet : AggregateRoot
{
    private Wallet() { }

    /// <summary>Yeni bir cuzdan olusturur (verilen UserId ile).</summary>
    /// <remarks>Handler: AddCardCommandHandler</remarks>
    public static Wallet Create(Guid userId) => new() { UserId = userId };

    public Guid UserId { get; private set; }

    [JsonProperty("Cards")] private List<SavedCard> _cards = new();

    /// <summary>Kayitli kartlari salt-okunur olarak doner.</summary>
    /// <remarks>Handler: GetDefaultCardBinQueryHandler, GetCardsQueryHandler</remarks>
    [JsonIgnore] public IReadOnlyList<SavedCard> Cards => _cards.AsReadOnly();

    /// <summary>Kart ekler; gecmis son-kullanma reddedilir (FR-009), dogrulama domain'de.</summary>
    /// <remarks>Handler: AddCardCommandHandler</remarks>
    // Kart ekler. Gecmis son-kullanma reddedilir (FR-009) — dogrulama domain'de, tokenizer stub'ta degil.
    public FeatureResultModel AddCard(SavedCard card, DateTimeOffset now)
    {
        if (!IsExpiryInFuture(card.ExpiryMonth, card.ExpiryYear, now))
            return FeatureResultModel.Error(
                new MessageItem { Property = nameof(card.ExpiryYear), Code = CustomerResourceConstants.INVALID_VALUE });

        _cards.Add(card);
        return FeatureResultModel.Ok();
    }

    /// <summary>Karti cikarir + token'ini doner (handler gateway'de best-effort revoke eder).</summary>
    /// <remarks>Handler: DeleteCardCommandHandler</remarks>
    // Karti cikarir + token'ini doner (handler gateway'de best-effort revoke eder — fail-open).
    public FeatureObjectResultModel<RemovedCard> RemoveCard(Guid cardId)
    {
        var card = _cards.FirstOrDefault(x => x.Id == cardId);
        if (card is null) return FeatureObjectResultModel<RemovedCard>.NotFound();
        _cards.Remove(card);
        return FeatureObjectResultModel<RemovedCard>.Ok(new RemovedCard { Token = card.Token });
    }

    /// <summary>Hedef karti varsayilan yapar; digerlerini temizler (≤1 varsayilan invariant).</summary>
    /// <remarks>Handler: SetDefaultCardCommandHandler</remarks>
    // ≤1 varsayilan invariant: hedef bulunur, digerleri false, hedef true (tek yazma — atomik).
    public FeatureResultModel SetDefaultCard(Guid cardId)
    {
        var target = _cards.FirstOrDefault(x => x.Id == cardId);
        if (target is null) return FeatureResultModel.NotFound();

        foreach (var card in _cards)
            card.SetDefault(card.Id == cardId);

        return FeatureResultModel.Ok();
    }

    /// <summary>Verilen ay/yil son-kullanmanin gelecekte olup olmadigini dogrular.</summary>
    /// <remarks>Handler: AddCardCommandHandler</remarks>
    // Handler tokenize'dan ONCE cagirir (gecmis expiry'de token uretmeyip orphan token'i onler);
    // aggregate AddCard da invariant olarak yeniden dogrular (defense-in-depth).
    public static bool IsExpiryInFuture(int month, int year, DateTimeOffset now)
    {
        if (month is < 1 or > 12) return false;
        if (year < now.Year) return false;
        if (year == now.Year && month < now.Month) return false;
        return true;
    }

    // RemoveCard'in dondurdugu revoke bilgisi (token'i handler'a tasir).
    public class RemovedCard
    {
        public string Token { get; set; } = default!;
    }
}