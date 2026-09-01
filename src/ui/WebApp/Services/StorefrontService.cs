
namespace WebApp.Services;

public class StorefrontService(
    IStorefrontRefitService storefrontRefitService,
    ILogger<StorefrontService> logger)
{
    public async Task<ServiceResult<PagedProductListViewModel>> GetProductsAsync(
        int pageNumber = 1, int pageSize = 12, Guid? categoryId = null,
        Guid? authorId = null, Guid? publisherId = null, string? q = null, string[]? specs = null)
    {
        var productsAsResult = await storefrontRefitService.GetProducts(pageNumber, pageSize, categoryId,
            authorId, publisherId, string.IsNullOrWhiteSpace(q) ? null : q,
            specs is { Length: > 0 } ? specs : null);

        // 011 FR-006: boş vitrin / aralık dışı sayfa API'de NotFound(400) döner; UI boş durum gösterir.
        if (productsAsResult.StatusCode == HttpStatusCode.BadRequest)
            return ServiceResult<PagedProductListViewModel>.Success(PagedProductListViewModel.Empty(pageNumber));

        if (!productsAsResult.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(productsAsResult.Error);
            return ServiceResult<PagedProductListViewModel>.Error(
                "Failed to retrieve product data. Please try again later.");
        }

        var content = productsAsResult.Content!;
        var products = content.Data
            .Select(p => new StorefrontProductViewModel(p.ProductId, p.Name, p.Description,
                (p.Authors ?? []).Select(a => new AuthorViewModel(a.Id, a.Name)).ToList(),
                p.Publisher, p.PublisherId,
                p.Price, p.ImageUrl, p.StockQuantity, p.IsInStock, p.Category,
                p.CategoryId,
                RatingAverage: p.RatingAverage, RatingCount: p.RatingCount,
                VariantCount: p.VariantCount))
            .ToList();

        return ServiceResult<PagedProductListViewModel>.Success(new PagedProductListViewModel(
            products, content.PageNumber, content.PageCount, content.TotalItemCount));
    }

    // 054: kişisel ana sayfa feed'i (yalnız kimlikli kullanıcı çağırır — anonim için WebApp hiç gelmez).
    // Boş liste geçerli sonuçtur (sinyalsiz kullanıcı) — UI boş durumu çizer; hata da boş duruma düşer
    // (ana sayfa feed hatasıyla kırılmaz).
    public async Task<List<StorefrontProductViewModel>> GetPersonalFeedAsync()
    {
        var response = await storefrontRefitService.GetPersonalFeed();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return [];
        }

        if (response.Content?.Data is null)
            return [];

        return response.Content.Data
            .Select(p => new StorefrontProductViewModel(p.ProductId, p.Name, p.Description,
                (p.Authors ?? []).Select(a => new AuthorViewModel(a.Id, a.Name)).ToList(),
                p.Publisher, p.PublisherId,
                p.Price, p.ImageUrl, p.StockQuantity, p.IsInStock, p.Category,
                p.CategoryId,
                RatingAverage: p.RatingAverage, RatingCount: p.RatingCount,
                VariantCount: p.VariantCount))
            .ToList();
    }

    // 016: filtre seçenekleri (facet) — hata durumunda boş seçenekle devam edilir (liste yine çizilir).
    public async Task<FilterOptionsViewModel> GetFilterOptionsAsync()
    {
        var response = await storefrontRefitService.GetFilterOptions();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return FilterOptionsViewModel.Empty;
        }

        var content = response.Content!;
        return new FilterOptionsViewModel(
            content.Categories.Select(x => new FilterOptionViewModel(x.Id, x.Name)).ToList(),
            (content.Authors ?? []).Select(x => new FilterOptionViewModel(x.Id, x.Name)).ToList(),
            (content.Publishers ?? []).Select(x => new FilterOptionViewModel(x.Id, x.Name)).ToList(),
            (content.Specifications ?? []).Select(s => new SpecFacetViewModel(s.Name,
                s.Options.Select(o => new SpecFacetOptionViewModel(o.Name, o.Count, $"{s.Name}|{o.Name}"))
                    .ToList())).ToList());
    }

    // Ürün detayı vitrinden (read model) okunur — Catalog'a gidilmez. Kısmi satır (Name/Price
    // henüz raporlanmadı) veya silinmiş ürün alıcıya "bulunamadı" davranır.
    public async Task<ServiceResult<StorefrontProductViewModel>> GetProductAsync(Guid productId)
    {
        var response = await storefrontRefitService.GetProduct(productId);

        if (!response.IsSuccessStatusCode)
            return ServiceResult<StorefrontProductViewModel>.FailFromProblemDetails(response.Error);

        var p = response.Content!;
        if (p.IsDeleted || p.Name is null || p.Price is null)
            return ServiceResult<StorefrontProductViewModel>.Error(
                "Ürün bulunamadı.", "Ürün vitrinde değil veya henüz yayınlanmadı.");

        return ServiceResult<StorefrontProductViewModel>.Success(new StorefrontProductViewModel(
            p.ProductId, p.Name, p.Description ?? string.Empty,
            (p.Authors ?? []).Select(a => new AuthorViewModel(a.Id, a.Name)).ToList(),
            p.Publisher, p.PublisherId,
            p.Price.Value, p.ImageUrl, p.StockQuantity, p.IsInStock, p.Category,
            p.CategoryId,
            (p.Specs ?? []).Select(s => new ProductSpecViewModel(s.Attribute, s.Option)).ToList(),
            RatingAverage: p.RatingAverage, RatingCount: p.RatingCount));
    }

    // 045: ürünün varyant ailesi; ailesiz/tek üye/404 → null (WebApp seçici çizmez).
    public async Task<VariantFamilyViewModel?> GetFamilyAsync(Guid productId)
    {
        var response = await storefrontRefitService.GetFamily(productId);
        if (!response.IsSuccessStatusCode || response.Content is null)
            return null;

        var f = response.Content;
        if (f.Members.Count <= 1)
            return null; // tek üye → seçici yok

        var members = f.Members.Select(m => new VariantMemberViewModel(
            m.ProductId, m.Name, m.Price, m.IsInStock, m.IsCurrent,
            m.Specs.ToDictionary(s => s.Attribute, s => s.Option))).ToList();
        var axes = f.Axes.Select(a => new VariantAxisViewModel(a.Attribute, a.Options)).ToList();

        return new VariantFamilyViewModel(axes, members);
    }
}