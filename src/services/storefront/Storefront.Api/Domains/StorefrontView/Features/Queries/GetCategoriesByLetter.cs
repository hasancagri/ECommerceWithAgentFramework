namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// Kategori dizini harf dilimi: harf seçilince yalnız o harfle başlayan kategoriler döner (yaprak —
// read-model hiyerarşi taşımaz). Harf daraltması DB'de; tekilleştirme bellekte.
// Cache "filters" tag'ini paylaşır (ürün değişiminde boşalır) + 60sn TTL güvenlik ağı.
public static class GetCategoriesByLetter
{
    [Cached("filters", 60)]
    public record GetCategoriesByLetterQuery(string Letter);

    public class CategoryIndexItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }

    // Saf, test edilebilir çekirdek: "A".."Z" veya "#" (harf-dışı) — büyük harfe normalize edilir,
    // geçersiz girdi null (çağıran INVALID_VALUE döner).
    public static string? NormalizeLetter(string? letter)
    {
        var t = letter?.Trim().ToUpperInvariant();
        if (t is not { Length: 1 }) return null;
        return t[0] is (>= 'A' and <= 'Z') or '#' ? t : null;
    }

    // Saf çekirdek: SQL tarafındaki eşleşmenin bellek-içi ikizi ("#" = A-Z ile başlamayan).
    public static bool MatchesLetter(string? name, string letter)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var c = char.ToUpperInvariant(name[0]);
        var isAz = c is >= 'A' and <= 'Z';
        return letter == "#" ? !isAz : c == letter[0];
    }

    public class GetCategoriesByLetterQueryHandler
    {
        public async Task<FeatureListResultModel<CategoryIndexItemResponse>> Handle(
            GetCategoriesByLetterQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var letter = NormalizeLetter(query.Letter);
            if (letter is null)
                return FeatureListResultModel<CategoryIndexItemResponse>.Error(
                    new MessageItem { Code = StorefrontResourceConstants.INVALID_VALUE });

            // Liste ile aynı satılabilirlik (dolu-satır) filtresi; harf daraltması DB'de.
            var sellable = session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null && x.CategoryId != null);

            sellable = letter == "#"
                ? sellable.Where(x => x.MatchesSql("d.data ->> 'Category' !~* '^[a-z]'"))
                : sellable.Where(x => x.Category!.StartsWith(letter, StringComparison.OrdinalIgnoreCase));

            var rows = await sellable.ToListAsync(ct);

            var categories = rows
                .Where(x => MatchesLetter(x.Category, letter))
                .GroupBy(x => x.CategoryId!.Value)
                .Select(g => new CategoryIndexItemResponse { Id = g.Key, Name = g.First().Category! })
                .OrderBy(x => x.Name)
                .ToList();

            return FeatureListResultModel<CategoryIndexItemResponse>.Ok(categories);
        }
    }
}

public static class GetCategoriesByLetterEndpoint
{
    public static RouteGroupBuilder GetCategoriesByLetterGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (IMessageBus bus, string letter) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetCategoriesByLetter.CategoryIndexItemResponse>>(
                    new GetCategoriesByLetter.GetCategoriesByLetterQuery(letter));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetCategoriesByLetter")
            .MapToApiVersion(1, 0)
            .Produces<FeatureListResultModel<GetCategoriesByLetter.CategoryIndexItemResponse>>()
            .AllowAnonymous();

        return group;
    }
}