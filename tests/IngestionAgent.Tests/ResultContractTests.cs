using System.Text.Json;
using IngestionAgent.Workflows;
using Microsoft.Extensions.AI;

namespace IngestionAgent.Tests;

// WriterResult sözleşme testleri (015/T020): LLM'in structured-output çıktısı bu tiplere çözülür;
// seçenekler MAF RunAsync<T> ile aynı (AIJsonUtilities varsayılanları). Bozuk/eksik çıktı sessiz
// başarıya değil hataya gitmeli (SC-002) — eksik ProductId'yi executor hataya çevirir (FR-006).
public class ResultContractTests
{
    private static readonly JsonSerializerOptions Options = AIJsonUtilities.DefaultOptions;

    [Fact]
    public void CamelCaseSuccess_MapsToCatalogWriterResult()
    {
        var r = JsonSerializer.Deserialize<CatalogWriterResult>(
            """{"isSuccess":true,"error":null,"productId":"7c9e6679-7425-40de-944b-e07fc1f90ae7"}""", Options);

        r.ShouldNotBeNull();
        r.IsSuccess.ShouldBeTrue();
        r.Error.ShouldBeNull();
        r.ProductId.ShouldBe(Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"));
    }

    [Fact]
    public void FailureWithCode_MapsError()
    {
        var r = JsonSerializer.Deserialize<WriterResult>(
            """{"isSuccess":false,"error":"DUPLICATE_SKU"}""", Options);

        r.ShouldNotBeNull();
        r.IsSuccess.ShouldBeFalse();
        r.Error.ShouldBe("DUPLICATE_SKU");
    }

    [Fact]
    public void SuccessWithoutProductId_LeavesNull_ExecutorTreatsAsFailure()
    {
        // Sahte-başarı senaryosu: model tool'suz "ok" derse productId üretemez → null kalır;
        // CatalogWriteExecutor bunu PRODUCT_ID_MISSING hatasına çevirir (burada sözleşme kanıtı).
        var r = JsonSerializer.Deserialize<CatalogWriterResult>(
            """{"isSuccess":true,"error":null}""", Options);

        r.ShouldNotBeNull();
        r.IsSuccess.ShouldBeTrue();
        r.ProductId.ShouldBeNull();
    }

    [Fact]
    public void MalformedJson_Throws()
    {
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<WriterResult>("bu json değil", Options));
    }

    [Fact]
    public void DescribeFailure_ComposesCodeAndOptionalDetail()
    {
        Failures.Describe("STOCK_WRITE_FAILED", null).ShouldBe("STOCK_WRITE_FAILED");
        Failures.Describe("STOCK_WRITE_FAILED", "bağlantı reddedildi")
            .ShouldBe("STOCK_WRITE_FAILED: bağlantı reddedildi");
    }
}