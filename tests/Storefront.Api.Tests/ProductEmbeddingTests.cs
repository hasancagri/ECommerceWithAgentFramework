namespace Storefront.Api.Tests;

// 019 US2/US4: arama metni kurma (null alanlar atlanır) + TextHash değişim tespiti —
// hash aynıysa embedding yeniden üretilmez (FR-013/SC-004).
public class ProductEmbeddingTests
{
    // --- BuildSearchText ---

    [Fact]
    public void BuildSearchText_JoinsAllPartsWithNewline()
    {
        var text = ProductEmbedding.BuildSearchText("Kar Botu", "Kar ve buzda kaymaz taban", "Nike", "Ayakkabı");

        text.ShouldBe("Kar Botu\nKar ve buzda kaymaz taban\nNike\nAyakkabı");
    }

    [Fact]
    public void BuildSearchText_SkipsNullAndEmptyParts()
    {
        var text = ProductEmbedding.BuildSearchText("Kar Botu", null, "  ", "Ayakkabı");

        text.ShouldBe("Kar Botu\nAyakkabı");
    }

    [Fact]
    public void BuildSearchText_AllPartsMissing_ReturnsNull()
    {
        ProductEmbedding.BuildSearchText(null, null, null, null).ShouldBeNull();
    }

    // --- ComputeTextHash: değişim tespiti (FR-013) ---

    [Fact]
    public void ComputeTextHash_SameText_ProducesSameHash()
    {
        var first = ProductEmbedding.ComputeTextHash("Kar Botu\nAyakkabı");
        var second = ProductEmbedding.ComputeTextHash("Kar Botu\nAyakkabı");

        first.ShouldBe(second);
    }

    [Fact]
    public void ComputeTextHash_ChangedText_ProducesDifferentHash()
    {
        var original = ProductEmbedding.ComputeTextHash(
            ProductEmbedding.BuildSearchText("Kar Botu", "Eski açıklama", "Nike", "Ayakkabı")!);
        var changed = ProductEmbedding.ComputeTextHash(
            ProductEmbedding.BuildSearchText("Kar Botu", "Yeni açıklama", "Nike", "Ayakkabı")!);

        original.ShouldNotBe(changed);
    }

    // --- Create / Refresh ---

    [Fact]
    public void Create_SetsAllFields()
    {
        var productId = Guid.NewGuid();
        float[] vector = [0.1f, 0.2f];

        var embedding = ProductEmbedding.Create(productId, "hash-1", vector);

        embedding.ProductId.ShouldBe(productId);
        embedding.TextHash.ShouldBe("hash-1");
        embedding.Embedding.ShouldBe(vector);
        embedding.UpdatedTime.ShouldNotBe(default);
    }

    [Fact]
    public void Refresh_ReplacesHashAndVector()
    {
        var embedding = ProductEmbedding.Create(Guid.NewGuid(), "hash-1", [0.1f]);
        var firstUpdate = embedding.UpdatedTime;

        embedding.Refresh("hash-2", [0.9f]);

        embedding.TextHash.ShouldBe("hash-2");
        embedding.Embedding.ShouldBe(new[] { 0.9f });
        embedding.UpdatedTime.ShouldBeGreaterThanOrEqualTo(firstUpdate);
    }

    // --- US4: aynı metin → üretim gerekmez kararı hash karşılaştırmasıyla verilir ---

    [Fact]
    public void SameSearchText_HashMatchesStoredHash_NoRegenerationNeeded()
    {
        var text = ProductEmbedding.BuildSearchText("Kar Botu", "Kaymaz taban", "Nike", "Ayakkabı")!;
        var stored = ProductEmbedding.Create(Guid.NewGuid(), ProductEmbedding.ComputeTextHash(text), [0.1f]);

        // Yalnız stok değişti: arama metni aynı kaldı → hash eşit, üretim atlanır (SC-004).
        var rebuilt = ProductEmbedding.BuildSearchText("Kar Botu", "Kaymaz taban", "Nike", "Ayakkabı")!;

        ProductEmbedding.ComputeTextHash(rebuilt).ShouldBe(stored.TextHash);
    }
}