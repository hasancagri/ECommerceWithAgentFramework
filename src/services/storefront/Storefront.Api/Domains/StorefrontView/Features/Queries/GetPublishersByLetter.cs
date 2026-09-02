namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// Yayınevi dizini harf dilimi (kitapyurdu kalıbı): sayfa ilk açılışta veri ÇEKMEZ, harf seçilince
// yalnız o harfle başlayan yayınevleri döner. Harf daraltması DB'de (ILIKE / regex) — tüm-facet
// (GetStorefrontFilterOptions) bellek-içi kurulumundan bilinçli ayrı slice.
// Cache "filters" tag'ini paylaşır: ürün değişiminde harf listeleri de boşalır + 60sn TTL güvenlik ağı.
public static class GetPublishersByLetter
{
    [Cached("filters", 60)]
    public record GetPublishersByLetterQuery(string Letter);

    public class PublisherIndexItemResponse
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

    public class GetPublishersByLetterQueryHandler
    {
        public async Task<FeatureListResultModel<PublisherIndexItemResponse>> Handle(
            GetPublishersByLetterQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var letter = NormalizeLetter(query.Letter);
            if (letter is null)
                return FeatureListResultModel<PublisherIndexItemResponse>.Error(
                    new MessageItem { Code = StorefrontResourceConstants.INVALID_VALUE });

            // Liste ile aynı satılabilirlik (dolu-satır) filtresi; harf daraltması DB'de.
            var sellable = session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null && x.PublisherId != null);

            sellable = letter == "#"
                ? sellable.Where(x => x.MatchesSql("d.data ->> 'Publisher' !~* '^[a-z]'"))
                : sellable.Where(x => x.Publisher!.StartsWith(letter, StringComparison.OrdinalIgnoreCase));

            var rows = await sellable.ToListAsync(ct);

            var publishers = rows
                .Where(x => MatchesLetter(x.Publisher, letter))
                .GroupBy(x => x.PublisherId!.Value)
                .Select(g => new PublisherIndexItemResponse { Id = g.Key, Name = g.First().Publisher! })
                .OrderBy(x => x.Name)
                .ToList();

            return FeatureListResultModel<PublisherIndexItemResponse>.Ok(publishers);
        }
    }
}

public static class GetPublishersByLetterEndpoint
{
    public static RouteGroupBuilder GetPublishersByLetterGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/publishers", async (IMessageBus bus, string letter) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetPublishersByLetter.PublisherIndexItemResponse>>(
                    new GetPublishersByLetter.GetPublishersByLetterQuery(letter));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetPublishersByLetter")
            .MapToApiVersion(1, 0)
            .Produces<FeatureListResultModel<GetPublishersByLetter.PublisherIndexItemResponse>>()
            .AllowAnonymous();

        return group;
    }
}