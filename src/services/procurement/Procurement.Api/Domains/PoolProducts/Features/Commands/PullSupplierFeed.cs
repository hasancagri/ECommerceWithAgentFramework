using Procurement.Api.Infrastructure.Feeds;

namespace Procurement.Api.Domains.PoolProducts.Features.Commands;

// Tek tedarikçinin feed'ini çekip havuza işler (satır başına upsert + hash-diff + delist taraması).
// Değişen barkodların yayın kararı ayrı slice'a (PublishPoolProduct) lokal durable kuyrukla devredilir:
// outbox sayesinde mesajlar havuz yazımıyla AYNI tx'te commit edilir (yarım-iş penceresi yok).
public static class PullSupplierFeed
{
    public record PullSupplierFeedCommand(string SupplierCode);

    public class PullSupplierFeedResponse
    {
        public int Processed { get; set; }
        public int Rejected { get; set; }
        public int Changed { get; set; }
        public int Delisted { get; set; }
    }

    [Transactional]
    public class PullSupplierFeedCommandHandler
    {
        public async Task<FeatureObjectResultModel<PullSupplierFeedResponse>> Handle(
            PullSupplierFeedCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            SupplierFeedClient feedClient,
            ILogger<PullSupplierFeedCommandHandler> logger,
            CancellationToken ct)
        {
            var supplier = await session.Query<Supplier>()
                .FirstOrDefaultAsync(s => s.Code == cmd.SupplierCode, ct);
            if (supplier is null)
                return FeatureObjectResultModel<PullSupplierFeedResponse>.Error(new MessageItem
                { Property = nameof(cmd.SupplierCode), Code = ProcurementResourceConstants.SUPPLIER_CODE_REQUIRED });

            var rows = await feedClient.GetFeedAsync(cmd.SupplierCode, ct);
            var response = new PullSupplierFeedResponse();
            var changedBarcodes = new List<string>();

            // Tek tur yükleme: barkod başına LoadAsync yerine toplu okuma (3000+ satır).
            var barcodes = rows.Where(r => !string.IsNullOrWhiteSpace(r.Barcode))
                .Select(r => r.Barcode).Distinct().ToList();
            var existing = (await session.LoadManyAsync<PoolProduct>(ct, barcodes))
                .ToDictionary(p => p.Barcode);

            foreach (var row in rows)
            {
                // FR-010: barkodsuz satır havuz girişinde reddedilir + loglanır (AI barkod ÜRETMEZ).
                if (string.IsNullOrWhiteSpace(row.Barcode))
                {
                    logger.LogWarning("Barkodsuz satır reddedildi: {SupplierCode}/{Sku}", cmd.SupplierCode, row.SupplierSku);
                    response.Rejected++;
                    continue;
                }

                // Kategori eşlemesi: eşlenemeyen ad satırı DÜŞÜRMEZ — kategorisiz havuza girer, enrich tamamlar.
                var mapping = supplier.ResolveCategory(row.Category);
                var listingRow = ListingRow.Create(
                    row.SupplierSku, row.Name, row.Description, row.Brand, row.Category,
                    mapping.IsSuccess ? mapping.Data!.CanonicalCategory : null,
                    mapping.IsSuccess ? mapping.Data!.CanonicalSubCategory : null,
                    row.Price, row.Stock,
                    RowDimensions.Create(row.Weight, row.Length, row.Width, row.Height));

                if (!existing.TryGetValue(row.Barcode, out var product))
                {
                    var created = PoolProduct.Create(row.Barcode);
                    if (!created.IsSuccess)
                    {
                        logger.LogWarning("Havuz kaydı açılamadı: {Barcode}", row.Barcode);
                        response.Rejected++;
                        continue;
                    }

                    product = created.Data!;
                    existing[row.Barcode] = product;
                }

                var upsert = product.UpsertListing(supplier.Id, supplier.Priority, listingRow);
                if (!upsert.IsSuccess)
                {
                    logger.LogWarning("Satır reddedildi ({Barcode}): {Codes}", row.Barcode,
                        string.Join(",", upsert.Messages.Select(m => m.Code)));
                    response.Rejected++;
                    continue;
                }

                response.Processed++;
                if (upsert.Data == ListingChange.Unchanged)
                {
                    session.Store(product); // LastSeenUtc tazelendi; yayın tetiklenmez (hash aynı)
                    continue;
                }

                product.RebuildCanonical();
                session.Store(product);
                response.Changed++;
                changedBarcodes.Add(product.Barcode);
            }

            // Delist taraması: bu tedarikçinin havuzda görünen ama feed'de OLMAYAN satırları (full snapshot).
            var feedBarcodes = barcodes.ToHashSet();
            var supplierProducts = await session.Query<PoolProduct>()
                .Where(p => p.Listings.Any(l => l.SupplierId == supplier.Id))
                .ToListAsync(ct);
            foreach (var product in supplierProducts)
            {
                var listing = product.Listings.First(l => l.SupplierId == supplier.Id);
                if (listing.IsDelisted || feedBarcodes.Contains(product.Barcode))
                    continue;

                product.MarkDelisted(supplier.Id);
                product.RebuildCanonical();
                session.Store(product);
                response.Delisted++;
                changedBarcodes.Add(product.Barcode);
            }

            // Yayın + enrich kararları commit-sonrası işlenir (durable lokal kuyruk — outbox aynı tx'te yazar).
            // Enrich tetiği yalnız DEĞİŞEN ve eksik kalan barkodlar için; taze cache varsa AI'ya gidilmez (FR-009).
            var enrichCount = 0;
            foreach (var barcode in changedBarcodes.Distinct())
            {
                var product = existing.TryGetValue(barcode, out var p)
                    ? p
                    : supplierProducts.First(sp => sp.Barcode == barcode);
                if (product.NeedsEnrichment)
                {
                    if (!product.HasFreshEnrichment)
                    {
                        await bus.PublishAsync(new EnrichPoolProduct.EnrichPoolProductCommand(barcode));
                        enrichCount++;
                    }
                    continue; // eksik içerik yayınlanmaz (FR-011); yayını enrich zinciri tetikler
                }

                await bus.PublishAsync(new PublishPoolProduct.PublishPoolProductCommand(barcode));
            }
            if (enrichCount > 0)
                logger.LogInformation("Enrich kuyruğuna {Count} barkod verildi (yalnız eksik satırlar)", enrichCount);

            logger.LogInformation(
                "Pull {SupplierCode}: {Processed} işlendi, {Changed} değişti, {Delisted} delist, {Rejected} red",
                cmd.SupplierCode, response.Processed, response.Changed, response.Delisted, response.Rejected);
            return FeatureObjectResultModel<PullSupplierFeedResponse>.Ok(response);
        }
    }
}