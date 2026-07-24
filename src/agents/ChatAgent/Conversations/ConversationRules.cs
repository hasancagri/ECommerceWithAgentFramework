namespace ChatAgent.Conversations;

// Saf kurallar — framework tipi yok, IO yok; birim testler buraya bağlanır (T013).
public static class ConversationRules
{
    public const string DefaultTitle = "Yeni sohbet";
    public const int MaxTitleLength = 60;
    public const int DefaultContextWindowItems = 40;
    public const int DefaultAnonymousTtlHours = 24;

    // Başlık ilk kullanıcı mesajından türetilir; boşsa varsayılan, uzunsa kelime sınırında kırpılır.
    public static string DeriveTitle(string? firstUserText)
    {
        var text = firstUserText?.Trim();
        if (string.IsNullOrEmpty(text))
            return DefaultTitle;

        text = text.ReplaceLineEndings(" ");
        if (text.Length <= MaxTitleLength)
            return text;

        var cut = text.LastIndexOf(' ', MaxTitleLength);
        return string.Concat((cut > 0 ? text[..cut] : text[..MaxTitleLength]).TrimEnd(), "…");
    }

    // Modele giden pencere: kronolojik listenin SON windowSize öğesi, sıra korunarak (FR-005).
    public static IReadOnlyList<T> TakeContextWindow<T>(IReadOnlyList<T> chronological, int windowSize)
    {
        if (windowSize <= 0)
            windowSize = DefaultContextWindowItems;

        return chronological.Count <= windowSize
            ? chronological
            : chronological.Skip(chronological.Count - windowSize).ToList();
    }

    // Anonim TTL: yalnız sahipsiz VE aktivitesi eşikten eski konuşmalar süpürülür (FR-008/009).
    public static bool IsExpiredAnonymous(
        string? ownerUserId, DateTimeOffset lastActivity, DateTimeOffset now, TimeSpan ttl)
        => ownerUserId is null && lastActivity < now - ttl;
}