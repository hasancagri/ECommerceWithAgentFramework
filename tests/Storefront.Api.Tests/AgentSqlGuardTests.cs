namespace Storefront.Api.Tests;

// 069 T006 (İLKE VI test-first): saf bekçi — yorum soyma, tek statement, SELECT/WITH-dışı ret,
// yasak kelime (kelime-sınırlı + literal-dışı), ilişki whitelist (unnest/CTE pozitif), uzunluk,
// LIMIT sarmalama. Kontrat: contracts/query-storefront-tool.md.
public class AgentSqlGuardTests
{
    private const int MaxLen = 4000;
    private const int MaxRows = 50;

    private static ResultDomain<AgentSqlGuard.GuardedQuery> Validate(string sql) =>
        AgentSqlGuard.Validate(sql, MaxLen, MaxRows);

    private static string CodeOf(ResultDomain<AgentSqlGuard.GuardedQuery> result) =>
        result.Messages!.Single().Code!;

    // --- geçerli sorgular GEÇER + tavan sarmalanır ---

    [Fact]
    public void Simple_select_passes_and_wraps_limit()
    {
        var result = Validate("select name, price from storefront_sellable where price < 100");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.WrappedSql.ShouldContain("limit 51"); // MaxRows+1 (Truncated tespiti)
        result.Data.WrappedSql.ShouldContain("storefront_sellable");
    }

    [Fact]
    public void With_cte_passes_and_cte_alias_is_known_relation()
    {
        var result = Validate(
            "with cheap as (select * from storefront_sellable where price < 50) " +
            "select category, count(*) from cheap group by category");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Trailing_semicolon_is_tolerated()
    {
        Validate("select name from storefront_sellable;").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Schema_qualified_view_passes()
    {
        Validate("select name from storefrontmanagement.storefront_sellable")
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Unnest_in_from_passes()
    {
        var result = Validate(
            "select name from storefront_sellable " +
            "where exists (select 1 from unnest(authors) a where a ilike '%wells%')");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Comma_cross_join_with_unnest_passes()
    {
        var result = Validate(
            "select a, count(*) from storefront_sellable s, unnest(s.authors) a group by a order by count(*) desc limit 1");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Jsonb_array_elements_in_from_passes()
    {
        var result = Validate(
            "select name from storefront_sellable " +
            "where exists (select 1 from jsonb_array_elements(specs) s where s->>'Attribute' ilike '%cilt%')");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Subquery_in_from_passes()
    {
        var result = Validate(
            "select * from (select category, avg(price) p from storefront_sellable group by category) q order by p");

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Similarity_subquery_pattern_passes()
    {
        var result = Validate(
            "select name, price from storefront_sellable " +
            "where product_id <> '0f8fad5b-d9cb-469f-a165-70867728950e' and embedding is not null " +
            "and embedding <=> (select embedding from storefront_sellable " +
            "where product_id = '0f8fad5b-d9cb-469f-a165-70867728950e') < 0.68 " +
            "order by 2 limit 5");

        result.IsSuccess.ShouldBeTrue();
    }

    // --- yanlış-pozitif tuzakları (U1/U2 bulguları) ---

    [Fact]
    public void Offset_does_not_trigger_forbidden_set_keyword()
    {
        Validate("select name from storefront_sellable order by name limit 20 offset 40")
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Forbidden_word_inside_string_literal_passes()
    {
        Validate("select name from storefront_sellable where name ilike '%drop%'")
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Semicolon_and_keywords_inside_comments_are_stripped()
    {
        Validate("select name from storefront_sellable -- drop table; yorum\n where price > 10")
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Order_by_comma_after_from_does_not_expect_relation()
    {
        Validate("select * from storefront_sellable order by price, name")
            .IsSuccess.ShouldBeTrue();
    }

    // --- retler ---

    [Fact]
    public void Second_statement_is_rejected()
    {
        var result = Validate("select 1 from storefront_sellable; drop view storefront_sellable");

        result.IsSuccess.ShouldBeFalse();
        CodeOf(result).ShouldBe(StorefrontResourceConstants.AgentSqlMultiStatement);
    }

    [Fact]
    public void Non_select_start_is_rejected()
    {
        var result = Validate("update storefront_sellable set price = 1");

        result.IsSuccess.ShouldBeFalse();
        CodeOf(result).ShouldBe(StorefrontResourceConstants.AgentSqlNotReadOnly);
    }

    [Fact]
    public void Empty_sql_is_rejected()
    {
        Validate("   ").IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("select pg_sleep(10) from storefront_sellable")]
    [InlineData("select * from storefront_sellable where pg_read_file('/etc/passwd') is not null")]
    [InlineData("select * from storefront_sellable for update")]
    [InlineData("select name into tmp_x from storefront_sellable")]
    public void Forbidden_keywords_are_rejected(string sql)
    {
        var result = Validate(sql);

        result.IsSuccess.ShouldBeFalse();
        CodeOf(result).ShouldBe(StorefrontResourceConstants.AgentSqlForbiddenKeyword);
    }

    [Fact]
    public void Unknown_relation_is_rejected()
    {
        var result = Validate("select * from mt_doc_userpurchase");

        result.IsSuccess.ShouldBeFalse();
        CodeOf(result).ShouldBe(StorefrontResourceConstants.AgentSqlUnknownRelation);
    }

    [Fact]
    public void Join_to_unknown_relation_is_rejected()
    {
        var result = Validate(
            "select * from storefront_sellable v join mt_doc_agentquerylog l on l.id = v.product_id");

        result.IsSuccess.ShouldBeFalse();
        CodeOf(result).ShouldBe(StorefrontResourceConstants.AgentSqlUnknownRelation);
    }

    [Fact]
    public void Too_long_sql_is_rejected()
    {
        var longSql = "select name from storefront_sellable where name ilike '%" +
                      new string('a', MaxLen) + "%'";

        var result = Validate(longSql);

        result.IsSuccess.ShouldBeFalse();
        CodeOf(result).ShouldBe(StorefrontResourceConstants.AgentSqlTooLong);
    }
}
