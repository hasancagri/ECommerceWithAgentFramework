using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using Procurement.Api.Seeding;

namespace Procurement.Api.Infrastructure.Enrichment;

// Enrich agent (R5): in-process Singleton ChatClientAgent — MCP TÜKETMEZ (kendi BC verisi),
// Temperature=0, structured JSON çıktı. YALNIZ eksik İÇERİK alanlarını üretir; barkod/ölçü/fiyat/stok
// İSTENMEZ ve çıktıda yapısal olarak yoktur (FR-010; aggregate guard'ı ayrıca reddeder).
public sealed class EnrichmentAgent
{
    // Structured çıktı sözleşmesi: yalnız içerik alanları. Kategori verilen listeden birebir seçilir.
    // 043: Specs — kapalı listeden seçilmiş (Attribute, Option) çiftleri; emin olunmayan boş bırakılır.
    public record SpecPick(string Attribute, string Option);

    public record EnrichmentOutput(string? Description, string? Category, string? SubCategory,
        List<SpecPick>? Specs);

    private const string Instructions =
        """
        Sen bir e-ticaret ürün içeriği tamamlayıcısısın. Sana bir ürünün MEVCUT alanları, EKSİK alan
        listesi ve kanonik kategori listesi verilir. YALNIZ eksik alanları üret; dolu alanlara null dön.
        Kurallar:
        - Açıklama eksikse: ürün adı ve markasına uygun, Türkçe, 1-2 cümlelik satış açıklaması yaz.
        - Kategori eksikse: verilen kanonik listeden ürüne EN uygun (Category, SubCategory) çiftini
          BİREBİR aynı yazımla seç; liste dışına asla çıkma.
        - Barkod, ölçü, ağırlık, fiyat, stok ASLA üretme — bunlar sorulmaz ve yanıt şemasında yoktur.
        - Özellikler (Specs): ürüne UYGUN attribute'lar için verilen kapalı listeden değer seç;
          liste dışına asla çıkma; emin olmadığın attribute'u hiç yazma. Ürünün zaten sahip olduğu
          attribute'lara dokunma (yalnız EKSİK attribute listesindekiler).
        """;

    private readonly ChatClientAgent _agent;

    public EnrichmentAgent(EnrichmentOptions options)
    {
        IChatClient chatClient = new OpenAIClient(options.ApiKey)
            .GetChatClient(options.Model)
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(o => o.ModelId = options.Model)
            .Build();

        _agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "procurement-enrichment",
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Temperature = 0, // yazma yolunda varyans istenmez (015/R6 mirası)
            },
        });
    }

    public async Task<EnrichmentOutput> CompleteAsync(
        string name, string brand, string? description, string? category,
        IReadOnlyList<string> rawCategoryHints,
        IReadOnlyList<string> existingSpecAttributes, CancellationToken ct)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(description)) missing.Add("Description");
        if (string.IsNullOrWhiteSpace(category)) missing.Add("Category+SubCategory");

        // 043: ürünün henüz taşımadığı attribute'lar kapalı listeleriyle prompt'a girer.
        var missingSpecs = CanonicalSpecs.Definitions
            .Where(d => !existingSpecAttributes.Contains(d.Attribute))
            .ToList();
        if (missingSpecs.Count > 0) missing.Add("Specs");

        var taxonomy = string.Join("\n", CanonicalTaxonomy.Pairs
            .Select(p => $"- {p.Category} | {p.SubCategory}"));
        var prompt =
            $"""
             Ürün adı: {name}
             Marka: {brand}
             Mevcut açıklama: {(string.IsNullOrWhiteSpace(description) ? "(YOK)" : description)}
             Mevcut kanonik kategori: {(string.IsNullOrWhiteSpace(category) ? "(YOK)" : category)}
             Tedarikçi ham kategori ipuçları: {(rawCategoryHints.Count == 0 ? "(yok)" : string.Join(", ", rawCategoryHints))}
             EKSİK alanlar: {string.Join(", ", missing)}
             Kanonik kategori listesi (Category | SubCategory):
             {taxonomy}
             EKSİK özellik attribute'ları ve kapalı seçenek listeleri:
             {string.Join("\n", missingSpecs.Select(d => $"- {d.Attribute}: {string.Join(", ", d.Options)}"))}
             """;

        var response = await _agent.RunAsync<EnrichmentOutput>(prompt, cancellationToken: ct);
        return response.Result;
    }
}
