namespace Storefront.Api;

// 067/068: pgvector kNN sorgusunun TEK evi — ham SQL projede yalnız burada yaşar (slice'lar tip-güvenli
// çağırır). Neden ham SQL: Marten.PgVector'un VectorSearchAsync'i ne aday-kümesi kısıtı (pre-filtering)
// ne mesafe eşiği destekliyor (research R2). DİKKAT (R9): 'Vector' PascalCase — Newtonsoft default
// casing; yanlış ad SESSİZCE boş sonuç üretir (canlıda doğrulandı). İleri seçenek: Marten
// IMethodCallParser ile CosineDistance LINQ operatörü — ORDER BY çevirisi araştırma ister.
public static class ProductEmbeddingKnnQuery
{
    /// <summary>
    /// Aday id kümesi İÇİNDE (pre-filtering) sorgu vektörüne kosinüs-en-yakın satırları döner;
    /// maxDistance eşiğini aşanlar elenir (SC-005 "alakasızı benzer diye sunma").
    /// </summary>
    public static Task<IReadOnlyList<Domains.StorefrontView.ProductDescriptionEmbedding>> NearestAsync(
        IQuerySession session,
        Guid[] candidateIds,
        ReadOnlySpan<float> queryVector,
        double maxDistance,
        int limit,
        CancellationToken ct)
    {
        var vectorLiteral = ToVectorLiteral(queryVector);
        return session.QueryAsync<Domains.StorefrontView.ProductDescriptionEmbedding>(
            "where id = ANY(?) and (data ->> 'Vector')::vector <=> CAST(? as vector) < ? " +
            "order by (data ->> 'Vector')::vector <=> CAST(? as vector) limit ?",
            ct,
            candidateIds,
            vectorLiteral,
            maxDistance,
            vectorLiteral,
            limit);
    }

    // pgvector metin formu: "[0.1,0.2,...]" (InvariantCulture şart). Metin-literal + CAST çünkü Weasel
    // Pgvector.Vector parametresini bind edemiyor ("Can't infer NpgsqlDbType", canlı bulgu).
    public static string ToVectorLiteral(ReadOnlySpan<float> vector)
    {
        var parts = new string[vector.Length];
        for (var i = 0; i < vector.Length; i++)
            parts[i] = vector[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"[{string.Join(',', parts)}]";
    }
}