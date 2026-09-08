namespace Storefront.Api.Tests;

// 069 T016 (İLKE VI test-first): {{EMBED:"metin"}} ayrıştırma + parametre ikamesi + bozuk sözdizimi
// reddi + vektör-literal üretimi (InvariantCulture). Kontrat: contracts/query-storefront-tool.md.
public class EmbedPlaceholderTests
{
    [Fact]
    public void No_placeholder_returns_sql_unchanged_with_no_texts()
    {
        var result = EmbedPlaceholder.Substitute("select name from storefront_sellable");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Sql.ShouldBe("select name from storefront_sellable");
        result.Data.Texts.ShouldBeEmpty();
    }

    [Fact]
    public void Single_placeholder_is_replaced_with_cast_parameter()
    {
        var result = EmbedPlaceholder.Substitute(
            "select name from storefront_sellable " +
            "where embedding <=> {{EMBED:\"kışın okunacak bilim kurgu\"}} < 0.68");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Sql.ShouldContain("CAST(@emb0 AS vector)");
        result.Data.Sql.ShouldNotContain("{{EMBED");
        result.Data.Texts.ShouldBe(["kışın okunacak bilim kurgu"]);
    }

    [Fact]
    public void Multiple_placeholders_get_ordered_parameters()
    {
        var result = EmbedPlaceholder.Substitute(
            "select embedding <=> {{EMBED:\"tema bir\"}} d1, embedding <=> {{EMBED:\"tema iki\"}} d2 " +
            "from storefront_sellable");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Sql.ShouldContain("@emb0");
        result.Data.Sql.ShouldContain("@emb1");
        result.Data.Texts.ShouldBe(["tema bir", "tema iki"]);
    }

    [Fact]
    public void Escaped_quotes_inside_text_are_unescaped()
    {
        var result = EmbedPlaceholder.Substitute(
            "select 1 from storefront_sellable where embedding <=> {{EMBED:\"kitap \\\"adı\\\" tema\"}} < 1");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Texts.ShouldBe(["kitap \"adı\" tema"]);
    }

    [Theory]
    [InlineData("select 1 where x <=> {{EMBED:kış}} < 1")]         // tırnaksız
    [InlineData("select 1 where x <=> {{EMBED:\"kış < 1")]          // kapanmamış
    [InlineData("select 1 where x <=> {{EMBED:\"\"}} < 1")]         // boş metin
    [InlineData("select 1 where x <=> {{embed:\"kış\"}} < 1")]      // küçük harf marker (bozuk say)
    public void Malformed_placeholder_is_rejected(string sql)
    {
        var result = EmbedPlaceholder.Substitute(sql);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.Single().Code.ShouldBe(StorefrontResourceConstants.AgentSqlBadEmbedPlaceholder);
    }

    [Fact]
    public void Vector_literal_uses_invariant_culture()
    {
        var literal = EmbedPlaceholder.ToVectorLiteral(new[] { 0.5f, -1.25f, 3f });

        literal.ShouldBe("[0.5,-1.25,3]");
    }
}
