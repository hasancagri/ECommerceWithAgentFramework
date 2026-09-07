using Shouldly;
using Storefront.Api.Domains.StorefrontView;
using Xunit;

namespace Storefront.Api.Tests;

// 067 İLKE VI (test-first): yeniden-embedding kararı — data-model.md karar tablosu.
// Karar saf metottur; handler ve backfill AYNI karara uyar (API çağrısı yalnız gerektiğinde).
public class StorefrontViewReembeddingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Yeni_aciklama_bos_ise_temsil_temizlenir(string? newDescription)
    {
        StorefrontView.DecideEmbedding(newDescription, "eski açıklama", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Clear);
    }

    [Fact]
    public void Yeni_aciklama_bos_ve_temsil_zaten_yok_ise_karar_yine_clear_idempotent()
    {
        StorefrontView.DecideEmbedding("", null, hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Clear);
    }

    [Fact]
    public void Aciklama_degismedi_ve_temsil_var_ise_dokunulmaz()
    {
        StorefrontView.DecideEmbedding("aynı metin", "aynı metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Keep);
    }

    [Fact]
    public void Aciklama_degismedi_ama_temsil_yok_ise_uretilir_backfill_ilk_kurulum()
    {
        StorefrontView.DecideEmbedding("aynı metin", "aynı metin", hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Aciklama_degisti_ise_temsil_yeniden_uretilir()
    {
        StorefrontView.DecideEmbedding("yeni metin", "eski metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Aciklama_bostan_doluya_gecince_uretilir_edge_case()
    {
        // Edge case (spec): import/description-fetch sonrası boş→dolu güncelleme.
        StorefrontView.DecideEmbedding("yeni gelen açıklama", null, hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Karar_buyuk_kucuk_harfe_duyarlidir_farkli_metin_farkli_temsildir()
    {
        // Ordinal karşılaştırma: metin gerçekten değiştiyse (case dahil) yeniden üretilir.
        StorefrontView.DecideEmbedding("Metin", "metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Temsil_dokumani_pk_ve_vektor_tasir()
    {
        // Temsil AYRI dokümanda (view şişmez); yaşam-döngüsü durumu taşımaz — görünürlük view filtresinde.
        var id = Guid.NewGuid();
        var doc = ProductDescriptionEmbedding.Create(id, [0.1f, 0.2f]);

        doc.ProductId.ShouldBe(id);
        doc.Vector.Length.ShouldBe(2);
    }
}