using Catalog.Api.Import;

namespace Catalog.Api.Tests.Import;

// 083 US1 domain-TDD (İLKE VI): staging satırının durum geçişleri — Pending→Processed / Pending→Failed,
// terminal koruması, ISBN zorunlu.
public class ImportRowTests
{
    private static ImportRow NewRow(string isbn = "9781442499713") =>
        ImportRow.Create(Guid.NewGuid(), isbn, "Kitap", "Yazar A;Yazar B", "Yayınevi",
            priceTry: 120m, stock: 100, "Roman", "Bilim Kurgu", "etiket1;etiket2",
            "Dil=TR; Sayfa=320", "açıklama", familyCode: null).Data!;

    [Fact]
    public void Create_ValidRow_IsPending()
    {
        var row = NewRow();

        row.Status.ShouldBe(ImportRowStatus.Pending);
        row.Isbn.ShouldBe("9781442499713");
        row.ProductId.ShouldBeNull();
        row.Error.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingIsbn_ReturnsError(string isbn)
    {
        var result = ImportRow.Create(Guid.NewGuid(), isbn, "Kitap", "Yazar", "Yayınevi",
            120m, 100, "Roman", "Bilim Kurgu", "", "", "", null);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void MarkProcessed_SetsStatusAndProductId()
    {
        var row = NewRow();
        var productId = Guid.NewGuid();

        var result = row.MarkProcessed(productId);

        result.IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(ImportRowStatus.Processed);
        row.ProductId.ShouldBe(productId);
        row.Error.ShouldBeNull();
    }

    [Fact]
    public void MarkFailed_SetsStatusAndError()
    {
        var row = NewRow();

        var result = row.MarkFailed("parse error");

        result.IsSuccess.ShouldBeTrue();
        row.Status.ShouldBe(ImportRowStatus.Failed);
        row.Error.ShouldBe("parse error");
    }

    [Fact]
    public void MarkProcessed_OnTerminalRow_ReturnsError()
    {
        var row = NewRow();
        row.MarkProcessed(Guid.NewGuid());

        var second = row.MarkProcessed(Guid.NewGuid());

        second.IsSuccess.ShouldBeFalse();
        row.Status.ShouldBe(ImportRowStatus.Processed);
    }

    [Fact]
    public void MarkFailed_OnProcessedRow_ReturnsError()
    {
        var row = NewRow();
        row.MarkProcessed(Guid.NewGuid());

        var failed = row.MarkFailed("late error");

        failed.IsSuccess.ShouldBeFalse();
        row.Status.ShouldBe(ImportRowStatus.Processed);
        row.Error.ShouldBeNull();
    }
}
