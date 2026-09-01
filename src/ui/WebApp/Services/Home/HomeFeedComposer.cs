using WebApp.Dto;
using WebApp.Services.Refit;
using WebApp.ViewModel;

namespace WebApp.Services.Home;

// 053 BFF orkestrasyon (FR-015/016 sınırı: profil-türetim Python, ranking Storefront, arayüz yalnız bağlar).
// Akış: reco-trainer'dan zevk profili oku → her cluster + discovery için Storefront ranking'e ver → kuşaklar.
// Profil yok/boş ya da her kuşak boşsa cold-start popüler vitrine düşer (FR-011 — boş/kırık sayfa yok).
// US2 (T029): share → oransal pageSize payı; excludeIds kuşaklar arası biriktirilir (tekrar önleme).
public class HomeFeedComposer(
    IRecoProfileRefitService profileClient,
    IStorefrontRecommendRefitService recommendClient,
    StorefrontService storefrontService,
    ILogger<HomeFeedComposer> logger)
{
    private const int ColdStartCount = 12;
    private const int PageSize = 12;        // waterfall load-more sayfa boyutu
    private const int TotalSlots = 36;      // feed toplam kart bütçesi (kuşaklara oransal dağıtılır)
    private const int MinShelfSize = 6;     // azınlık kuşağı taban kotası (görünür kalsın)
    private const int MaxShelfSize = 16;

    public async Task<HomeFeedViewModel> ComposeAsync(Guid? userId, Guid anonymousId)
    {
        TasteProfileDto? profile = null;
        try
        {
            var response = await profileClient.GetTasteProfile(userId, anonymousId);
            if (response.IsSuccessStatusCode) profile = response.Content;
        }
        catch (Exception ex)
        {
            // Kişiselleştirme yan-etkidir: reco-trainer erişilemezse alışveriş etkilenmez → cold-start.
            logger.LogWarning(ex, "Zevk profili okunamadı — cold-start vitrinine düşülüyor.");
        }

        if (profile is null || profile.Clusters.Count == 0)
            return await ColdStartAsync();

        var clusters = new List<ClusterDto>(profile.Clusters);
        if (profile.Discovery is not null) clusters.Add(profile.Discovery);

        var shelves = new List<HomeShelfViewModel>();
        var excludeIds = new List<Guid>();

        foreach (var cluster in clusters)
        {
            var pageSize = SlotsForShare(cluster.Share);
            var cards = await RecommendAsync(cluster, excludeIds, offset: 0, pageSize);
            if (cards.Count == 0) continue; // boş kuşak render edilmez (contract)

            excludeIds.AddRange(cards.Select(c => c.ProductId)); // sonraki kuşaklarda tekrar yok (SC-007)
            shelves.Add(new HomeShelfViewModel(cluster.Label, cluster.Reason, cards));
        }

        return shelves.Count == 0
            ? await ColdStartAsync()
            : new HomeFeedViewModel(shelves, IsColdStart: false);
    }

    // Oransal (calibrated) slot payı — argmax değil (FR-025); azınlık taban kotayla korunur (FR-008).
    private static int SlotsForShare(double share)
    {
        var slots = (int)Math.Round(share * TotalSlots);
        return Math.Clamp(slots, MinShelfSize, MaxShelfSize);
    }

    // 053 US2 (R9): stateless waterfall — kaydırmada bir kuşağın sonraki offset'i. Aday tükenince
    // (boş dönerse) popüler vitrinle doldurulur (zarifçe biter, kırık sayfa yok, FR-014).
    public async Task<List<StorefrontProductViewModel>> LoadMoreAsync(
        Guid? userId, Guid anonymousId, int shelfIndex, int offset)
    {
        TasteProfileDto? profile = null;
        try
        {
            var response = await profileClient.GetTasteProfile(userId, anonymousId);
            if (response.IsSuccessStatusCode) profile = response.Content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Load-more profil okunamadı — popüler doldurma.");
        }

        if (profile is null || profile.Clusters.Count == 0)
            return await PopularPageAsync(offset);

        var clusters = new List<ClusterDto>(profile.Clusters);
        if (profile.Discovery is not null) clusters.Add(profile.Discovery);

        if (shelfIndex < 0 || shelfIndex >= clusters.Count)
            return await PopularPageAsync(offset);

        var cards = await RecommendAsync(clusters[shelfIndex], excludeIds: [], offset, PageSize);
        return cards.Count > 0 ? cards : await PopularPageAsync(offset);
    }

    private async Task<List<StorefrontProductViewModel>> PopularPageAsync(int offset)
    {
        var page = (offset / PageSize) + 1;
        var popular = await storefrontService.GetProductsAsync(pageNumber: page, pageSize: PageSize);
        return popular.IsSuccess ? popular.Data!.Products : [];
    }

    private async Task<List<StorefrontProductViewModel>> RecommendAsync(
        ClusterDto cluster, List<Guid> excludeIds, int offset, int pageSize)
    {
        try
        {
            var request = new RecommendRequestDto(cluster.Attributes, Offset: offset, PageSize: pageSize,
                ExcludeIds: excludeIds.ToList());
            var response = await recommendClient.Recommend(request);
            if (!response.IsSuccessStatusCode || response.Content?.Data is null)
                return [];
            return response.Content.Data.Cards.Select(MapCard).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kuşak '{Label}' için ranking alınamadı — kuşak atlanıyor.", cluster.Label);
            return [];
        }
    }

    private async Task<HomeFeedViewModel> ColdStartAsync()
    {
        var popular = await storefrontService.GetProductsAsync(pageNumber: 1, pageSize: ColdStartCount);
        var products = popular.IsSuccess ? popular.Data!.Products : [];
        var shelf = new HomeShelfViewModel("Öne çıkan kitaplar", "Şu an popüler", products);
        return new HomeFeedViewModel([shelf], IsColdStart: true);
    }

    // Ranking kartı → mevcut kart view-model'i (aynı partial render edilir). Stok Storefront'ta filtrelendi (>0).
    private static StorefrontProductViewModel MapCard(ProductCardDto c) => new(
        c.ProductId, c.Name, string.Empty,
        c.Authors.Select(a => new AuthorViewModel(a.Id, a.Name)).ToList(),
        c.Publisher, c.PublisherId, c.Price, c.ImageUrl,
        StockQuantity: null, IsInStock: true, Category: null, CategoryId: null,
        RatingAverage: c.RatingAverage, RatingCount: c.RatingCount);
}
