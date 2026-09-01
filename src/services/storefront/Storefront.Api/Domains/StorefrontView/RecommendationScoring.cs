namespace Storefront.Api.Domains.StorefrontView;

// 053 US1 (İLKE VI, saf/test-first): bir kuşağın öznitelik ağırlıklarına göre kitap sıralama çekirdeği.
// score(book) = Σ weight_i (kitap i özniteliğini taşırsa) — ağırlıklı örtüşme. Aday çekme + stok/satış
// filtresi query slice'ta (Marten); burada YALNIZ saf skor + sıralama + excludeIds. MMR US2'de (T028) eklenir.
public static class RecommendationScoring
{
    // Storefront'un KENDİ öznitelik-ağırlık record'u (BC izolasyonu — Python/Shared tipini sızdırmaz).
    // Type ∈ {author, category}; period faz-1'de kullanılmaz (additive).
    public record AttributeWeight(string Type, string Value, decimal Weight);

    // Ağırlıklı örtüşme: kitabın taşıdığı her öznitelik için ağırlığı toplar (case-insensitive eşleşme).
    public static decimal Score(StorefrontView book, IReadOnlyList<AttributeWeight> attributes)
    {
        decimal total = 0m;
        foreach (var a in attributes)
        {
            var carries = a.Type switch
            {
                "author" => book.Authors.Any(x => string.Equals(x.Name, a.Value, StringComparison.OrdinalIgnoreCase)),
                "category" => string.Equals(book.Category, a.Value, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
            if (carries) total += a.Weight;
        }

        return total;
    }

    // Skor azalan sıralı aday listesi: excludeIds düşer, skor=0 (örtüşmesiz) elenir; eşitlikte puan sonra Id.
    public static IReadOnlyList<StorefrontView> Rank(
        IEnumerable<StorefrontView> candidates,
        IReadOnlyList<AttributeWeight> attributes,
        IReadOnlyCollection<Guid> excludeIds)
    {
        return candidates
            .Where(b => !excludeIds.Contains(b.ProductId))
            .Select(b => (Book: b, Score: Score(b, attributes)))
            .Where(x => x.Score > 0m)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Book.RatingAverage ?? 0m)
            .ThenBy(x => x.Book.ProductId)
            .Select(x => x.Book)
            .ToList();
    }

    // 053 US2 (FR-010): MMR çeşitlendirme — arka-arkaya birebir benzeri kırar. İlgi = sıralama konumu
    // (aday zaten skorlu geldi); benzerlik = içerik (yazar + kategori Jaccard). λ yüksek = ilgiye sadık,
    // düşük = çeşitliliğe. Greedy: her adımda λ·ilgi − (1−λ)·seçilenlere-max-benzerlik en yükseği seçilir.
    public static IReadOnlyList<StorefrontView> Diversify(IReadOnlyList<StorefrontView> ranked, decimal lambda)
    {
        if (ranked.Count <= 2) return ranked;

        var n = ranked.Count;
        // İlgi (relevance) sıralama konumundan: ilk öğe ≈1, son ≈ 1/n (skorun monoton vekili).
        var relevance = new Dictionary<Guid, decimal>();
        for (var i = 0; i < n; i++)
            relevance[ranked[i].ProductId] = (decimal)(n - i) / n;

        var remaining = ranked.ToList();
        var selected = new List<StorefrontView> { remaining[0] };
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            StorefrontView? best = null;
            var bestScore = decimal.MinValue;
            foreach (var candidate in remaining)
            {
                var maxSim = selected.Max(s => Similarity(candidate, s));
                var mmr = lambda * relevance[candidate.ProductId] - (1m - lambda) * maxSim;
                if (mmr > bestScore)
                {
                    bestScore = mmr;
                    best = candidate;
                }
            }

            selected.Add(best!);
            remaining.Remove(best!);
        }

        return selected;
    }

    // İçerik benzerliği: yazar adları + kategori token'ları üstünde Jaccard (|∩|/|∪|), [0,1].
    private static decimal Similarity(StorefrontView a, StorefrontView b)
    {
        var setA = Tokens(a);
        var setB = Tokens(b);
        if (setA.Count == 0 || setB.Count == 0) return 0m;

        var intersection = setA.Count(setB.Contains);
        var union = setA.Union(setB).Count();
        return union == 0 ? 0m : (decimal)intersection / union;
    }

    private static HashSet<string> Tokens(StorefrontView v)
    {
        var tokens = v.Authors.Select(x => "a:" + x.Name.ToLowerInvariant()).ToHashSet();
        if (!string.IsNullOrWhiteSpace(v.Category)) tokens.Add("c:" + v.Category.ToLowerInvariant());
        return tokens;
    }
}
