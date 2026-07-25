# Feed-Otoriteli Stok Supply Modeli — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Feed, `ProductStock` supply'ını rezervasyon/satışı ezmeden güncellesin; StockWrite
ingestion aşaması geri gelsin.

**Architecture:** `ProductStock` üç kavramı ayırır — `Quantity` (feed-otoriteli supply, korunan
alan adı), `_reservations` (sepet, TTL), `SoldInCycle` (döngü-içi satış). `Available = max(0,
Quantity − aktifRezerve − SoldInCycle)`. Feed yalnız `SetSupply` ile yazar ve `SoldInCycle`'ı
sıfırlar. Commit `SoldInCycle`'ı artırır, Quantity'ye dokunmaz.

**Tech Stack:** .NET 10, C#, Marten (document store), Wolverine (bus + RabbitMQ), gRPC,
xUnit + Shouldly (saf domain unit test).

## Global Constraints

- **Ön koşul: 012-stock-reservation MERGE edilmiş olmalı.** Bu plan onun `ProductStock`
  modelini (Quantity/OnHand, `_reservations`, `Commit`, `SetReservedQuantity`) temel alır.
- **Ön koşul: Anayasa amendment (Model C revizyonu) — Task 0.** Kod öncesi yapılır (repo kuralı).
- **Alan adı `Quantity` KORUNUR** (persisted; = supply). Marten şema migration'ı ve
  `StockChangedEvent`/DTO kontrat churn'ü olmasın diye rename yapılmaz; yalnız metot `SetQuantity`
  → `SetSupply` olur. Tasarımdaki "Supply" = repurpose edilmiş `Quantity` alanı.
- Result pattern: `ResultDomain`; invariant'lar aggregate içinde; satır ≤150 karakter (repo dokümanları).
- Test: yalnız saf domain unit (host/entegrasyon harness'ı yok); entegrasyon davranışı canlı doğrulanır.

---

### Task 0: Anayasa amendment — Model C revizyonu

**Files:**
- Modify: `.specify/memory/constitution.md` (Model C ifadesi)
- Modify: `CLAUDE.md` ("Model C" satırı, "Senkron RPC / stok" bölümü)

**Interfaces:** Yok (doküman). Sonraki task'lar bu amendment'a dayanır.

- [ ] **Step 1: constitution.md'de Model C'yi revize et**

Mevcut "tedarikçi feed'i stoğu EZMEZ; IngestionAgent StockWrite kaldırıldı" ifadesini şuna çevir:
"Tedarikçi feed'i `Supply`'ı (feed-otoriteli adet) yazar; rezervasyon ve döngü-içi satış
(`SoldInCycle`) ayrık tutulduğundan feed bunları ezmez. `Available = Supply − aktifRezerve −
SoldInCycle`. StockWrite ingestion aşaması bu ayrımla geri geldi."

- [ ] **Step 2: CLAUDE.md Model C satırını aynı yönde güncelle**

"**Model C:** ... `OnHand` yalnız Commit ve manuel `set_stock` ile değişir" satırını, yeni
modele göre düzelt: feed `set_stock` ile Supply'ı yazar; Commit `SoldInCycle`'ı artırır.

- [ ] **Step 3: Commit**

```bash
git add .specify/memory/constitution.md CLAUDE.md
git commit -m "docs(constitution): Model C revizyonu — feed Supply yazar, SoldInCycle ayrık"
```

---

### Task 1: ProductStock — SoldInCycle + SetSupply + Available

**Files:**
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs`
- Test: `tests/Stock.Api.Tests/ProductStockTests.cs`

**Interfaces:**
- Produces: `int SoldInCycle { get; private set; }`; `ResultDomain SetSupply(int quantity)`
  (Quantity set + SoldInCycle=0); `int AvailableAt(DateTimeOffset now)` (SoldInCycle'ı da düşer).
- Consumes: mevcut `Quantity`, `_reservations`, `ActiveReservedQuantity(now)`.

- [ ] **Step 1: Failing test — SetSupply SoldInCycle'ı sıfırlar + Available onu düşer**

`tests/Stock.Api.Tests/ProductStockTests.cs` içine ekle:

```csharp
[Fact]
public void SetSupply_ResetsSoldInCycle_AndAvailableSubtractsIt()
{
    var now = DateTimeOffset.UtcNow;
    var stock = ProductStock.Create(Guid.NewGuid(), 10);

    stock.SetSupply(8).IsSuccess.ShouldBeTrue();
    stock.OnHand.ShouldBe(8);
    stock.SoldInCycle.ShouldBe(0);
    stock.AvailableAt(now).ShouldBe(8);
}

[Fact]
public void SetSupply_Negative_IsRejected()
{
    var stock = ProductStock.Create(Guid.NewGuid(), 5);
    stock.SetSupply(-1).IsSuccess.ShouldBeFalse();
    stock.OnHand.ShouldBe(5); // değişmedi
}
```

- [ ] **Step 2: Run — fail (SetSupply/SoldInCycle yok)**

Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj --filter "FullyQualifiedName~SetSupply"`
Expected: FAIL (derlenmez — `SetSupply`/`SoldInCycle` tanımsız).

- [ ] **Step 3: Implement — SoldInCycle alanı + SetSupply + Available güncelle**

`ProductStock.cs`:
- `Quantity` altına ekle: `public int SoldInCycle { get; private set; }`
- Mevcut `SetQuantity` metodunu `SetSupply` olarak yeniden adlandır ve gövdeye `SoldInCycle = 0;`
  ekle (Quantity set edildikten sonra):

```csharp
public ResultDomain SetSupply(int quantity)
{
    if (quantity < 0)
        return ResultDomain.Error(new MessageItem
        {
            Property = nameof(Quantity),
            Code = StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE
        });

    Quantity = quantity;
    SoldInCycle = 0; // yeni feed gerçeği yansıtıyor kabul → döngü sayacı sıfırlanır
    return ResultDomain.Ok();
}
```
- `AvailableAt` ve `IsOversoldAt`'i SoldInCycle içerecek şekilde güncelle:

```csharp
public int AvailableAt(DateTimeOffset now) =>
    Math.Max(0, Quantity - ActiveReservedQuantity(now) - SoldInCycle);

public bool IsOversoldAt(DateTimeOffset now) =>
    Quantity < ActiveReservedQuantity(now) + SoldInCycle;
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj --filter "FullyQualifiedName~SetSupply"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs tests/Stock.Api.Tests/ProductStockTests.cs
git commit -m "feat(stock): SoldInCycle + SetSupply (feed-otoriteli supply, Available ayrımı)"
```

---

### Task 2: Commit — SoldInCycle'ı artırır, Quantity'ye dokunmaz

**Files:**
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs` (`Commit`)
- Test: `tests/Stock.Api.Tests/ProductStockTests.cs`

**Interfaces:**
- Consumes: Task 1'in `SoldInCycle`, `AvailableAt`.
- Produces: `Commit` davranışı — reserved birimi soldInCycle'a taşır, Quantity sabit kalır.

- [ ] **Step 1: Failing test — Commit Quantity'yi değiştirmez, SoldInCycle artar, Available sabit**

```csharp
[Fact]
public void Commit_MovesReservedToSoldInCycle_QuantityUnchanged()
{
    var now = DateTimeOffset.UtcNow;
    var userId = Guid.NewGuid();
    var stock = ProductStock.Create(Guid.NewGuid(), 10);

    stock.SetReservedQuantity(userId, 2, TimeSpan.FromMinutes(15), now).IsSuccess.ShouldBeTrue();
    stock.AvailableAt(now).ShouldBe(8);

    stock.Commit(userId, 2, now).IsSuccess.ShouldBeTrue();

    stock.OnHand.ShouldBe(10);        // Quantity DEĞİŞMEDİ
    stock.SoldInCycle.ShouldBe(2);
    stock.AvailableAt(now).ShouldBe(8); // commit öncesi/sonrası aynı
    stock.ReservedAt(now).ShouldBe(0);  // rezervasyon tüketildi
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj --filter "FullyQualifiedName~Commit_Moves"`
Expected: FAIL (bugün `Commit` `Quantity -= quantity` yapıyor → OnHand 8 döner).

- [ ] **Step 3: Implement — Commit gövdesinde `Quantity -= quantity;` yerine `SoldInCycle += quantity;`**

`ProductStock.Commit` içinde:
```csharp
-       Quantity -= quantity;
+       SoldInCycle += quantity; // birim: reserved → soldInCycle; Quantity (supply) sabit
```
Rezervasyon kaldırma/azaltma bloğu (`existing.Quantity == quantity ...`) aynen kalır.

- [ ] **Step 4: Run — pass**

Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj --filter "FullyQualifiedName~Commit_Moves"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs tests/Stock.Api.Tests/ProductStockTests.cs
git commit -m "feat(stock): Commit SoldInCycle'ı artırır, supply'a dokunmaz"
```

---

### Task 3: Rezervasyon tavanı SoldInCycle'ı sayar

**Files:**
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs` (`SetReservedQuantity`)
- Test: `tests/Stock.Api.Tests/ProductStockTests.cs`

**Interfaces:**
- Consumes: Task 2'nin `SoldInCycle`.
- Produces: yeni tavan `Quantity − ActiveReservedByOthers − SoldInCycle`.

- [ ] **Step 1: Failing test — soldInCycle sonrası yeni rezervasyon tavanı düşer**

```csharp
[Fact]
public void SetReservedQuantity_CeilingAccountsForSoldInCycle()
{
    var now = DateTimeOffset.UtcNow;
    var buyer = Guid.NewGuid();
    var other = Guid.NewGuid();
    var stock = ProductStock.Create(Guid.NewGuid(), 10);

    // other 2 sattı → SoldInCycle=2 (reserve+commit ile)
    stock.SetReservedQuantity(other, 2, TimeSpan.FromMinutes(15), now);
    stock.Commit(other, 2, now);

    // buyer artık en fazla 10-0-2 = 8 rezerve edebilir
    stock.SetReservedQuantity(buyer, 8, TimeSpan.FromMinutes(15), now).IsSuccess.ShouldBeTrue();
    stock.SetReservedQuantity(buyer, 9, TimeSpan.FromMinutes(15), now).IsSuccess.ShouldBeFalse();
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj --filter "FullyQualifiedName~CeilingAccounts"`
Expected: FAIL (bugünkü tavan SoldInCycle'ı saymaz → 9 kabul edilir).

- [ ] **Step 3: Implement — tavan kontrolüne `- SoldInCycle` ekle**

`SetReservedQuantity` içinde:
```csharp
-       if (quantity > Quantity - ActiveReservedByOthers(userId, now))
+       if (quantity > Quantity - ActiveReservedByOthers(userId, now) - SoldInCycle)
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj --filter "FullyQualifiedName~CeilingAccounts"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs tests/Stock.Api.Tests/ProductStockTests.cs
git commit -m "feat(stock): rezervasyon tavanı SoldInCycle'ı düşer"
```

---

### Task 4: Callsite'ları düzelt — SetSupply + gereksiz event kaldırma

**Files:**
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Agent/SetStock.cs`
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/SetStock.cs`
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/CommitStock.cs`

**Interfaces:**
- Consumes: Task 1 `SetSupply`. Bu, `SetQuantity` çağıran tüm handler'ları kırar → düzeltilir.

- [ ] **Step 1: Agent/SetStock ve Commands/SetStock handler'larında `SetQuantity` → `SetSupply`**

Her iki dosyada:
```csharp
-       var result = stock.SetQuantity(cmd.Quantity);
+       var result = stock.SetSupply(cmd.Quantity);
```
`StockChangedEvent(stock.ProductId, stock.Quantity)` yayını KALIR (Supply değişti → Storefront güncel).

- [ ] **Step 2: CommitStock — gereksiz StockChangedEvent yayınını kaldır**

`CommitStock` handler'ında Commit artık Quantity'yi değiştirmiyor (Supply sabit) → browse
görünürlüğü değişmez. Şu satırı sil:
```csharp
-       await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));
```
Response'taki `OnHand = stock.Quantity` kalır (fiziksel supply). gRPC reply'ın `Available` alanı
zaten `AvailableAt(now)` türetilenini taşır (Task 1 ile SoldInCycle'ı içerir) — değişiklik gerekmez.

- [ ] **Step 3: Build + mevcut testler**

Run: `dotnet build src/services/stock/Stock.Api/Stock.Api.csproj -v q` → 0 hata
Run: `dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj` → tümü yeşil
Expected: PASS (kırılan `SetQuantity` referansı kalmadı).

- [ ] **Step 4: Commit**

```bash
git add src/services/stock/Stock.Api/Domains/Stocks/Features/
git commit -m "refactor(stock): SetSupply callsite'ları + Commit'te gereksiz StockChangedEvent kaldırıldı"
```

---

### Task 5: IngestionAgent — StockWrite aşamasını geri getir

**Files:**
- Create: `src/agents/IngestionAgent/Workflows/02_StockWrite/StockWriterAgent.cs`
- Create: `src/agents/IngestionAgent/Workflows/02_StockWrite/StockWriteExecutor.cs`
- Rename: `Workflows/02_DiscountWrite/` → `Workflows/03_DiscountWrite/` (namespace `_02_` → `_03_`)
- Modify: `src/agents/IngestionAgent/Workflows/SupplierSnapshotHandler.cs`
- Modify: `src/agents/IngestionAgent/Program.cs` (StockWriterAgent DI kaydı — Catalog/Discount deseni)
- Test: `tests/IngestionAgent.Tests/WriteDecisionTests.cs`

**Interfaces:**
- Consumes: `RecordJob.ProductId` (CatalogWrite doldurur), `RecordJob.Message.StockQuantity`,
  `McpToolInvoker.CallAsync`, `set_stock` MCP tool.
- Produces: `StockWriteExecutor("stock-write")`; workflow zinciri Catalog→Stock→Discount.

- [ ] **Step 1: StockWriterAgent oluştur (tek tool: set_stock)**

`02_StockWrite/StockWriterAgent.cs`:
```csharp
namespace IngestionAgent.Workflows._02_StockWrite;

// Stok yazıcısı: yalnız stock MCP'sine bağlı, tek tool'u set_stock (feed-otoriteli supply).
public sealed class StockWriterAgent(McpConnection connection)
{
    public async Task<ToolOutcome> SetStockAsync(Guid productId, int quantity, CancellationToken ct)
    {
        return await connection.CallAsync("set_stock", new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["quantity"] = quantity
        }, ct);
    }
}
```

- [ ] **Step 2: StockWriteExecutor oluştur (her ingestion'da mutlak set; CatalogAction YOK)**

`02_StockWrite/StockWriteExecutor.cs`:
```csharp
namespace IngestionAgent.Workflows._02_StockWrite;

// Aşama 2 — stok: feed mutlak supply verir (SetSupply). Model C revizyonu: create/update
// ayrımı yok (CatalogAction kaldırıldı); her ingestion'da absolute set → idempotent, retry güvenli.
public sealed class StockWriteExecutor(StockWriterAgent stockAgent)
    : Executor<RecordJob, RecordJob>("stock-write")
{
    private const string StockWriteFailed = "STOCK_WRITE_FAILED";

    public override async ValueTask<RecordJob> HandleAsync(
        RecordJob job, IWorkflowContext context, CancellationToken cancellationToken)
    {
        if (job.Failure is not null)
            return job; // fail-fast guard; Completed'ı terminal DiscountWrite yazar

        try
        {
            var outcome = await stockAgent.SetStockAsync(
                job.ProductId!.Value, job.Message.StockQuantity, cancellationToken);

            if (!outcome.Success)
                job.Failure = Failures.Describe(StockWriteFailed, outcome.Error);
        }
        catch (Exception ex)
        {
            job.Failure = Failures.Describe(StockWriteFailed, ex.Message);
        }

        return job;
    }
}
```

- [ ] **Step 3: DiscountWrite'ı 03'e taşı (order netliği: 01 Catalog → 02 Stock → 03 Discount)**

`Workflows/02_DiscountWrite/` klasörünü `Workflows/03_DiscountWrite/` yap; iki dosyada namespace
`IngestionAgent.Workflows._02_DiscountWrite` → `._03_DiscountWrite`.

- [ ] **Step 4: SupplierSnapshotHandler — zincire StockWrite'ı ekle**

`SupplierSnapshotHandler.cs`:
```csharp
using IngestionAgent.Workflows._01_CatalogWrite;
using IngestionAgent.Workflows._02_StockWrite;
using IngestionAgent.Workflows._03_DiscountWrite;
...
    public static async Task Handle(
        IntegrationEvents.SupplierProductSnapshotReceived message,
        CatalogWriterAgent catalogAgent,
        StockWriterAgent stockAgent,
        DiscountWriterAgent discountAgent,
        CancellationToken ct)
    {
        var catalogWrite = new CatalogWriteExecutor(catalogAgent);
        var stockWrite = new StockWriteExecutor(stockAgent);
        var discountWrite = new DiscountWriteExecutor(discountAgent);

        var workflow = new WorkflowBuilder(catalogWrite)
            .AddEdge(catalogWrite, stockWrite)
            .AddEdge(stockWrite, discountWrite)
            .WithOutputFrom(discountWrite)
            .Build();
        ...
    }
```
Başlık yorumundaki "catalog → discount" ifadesini "catalog → stock → discount" yap; Model C
revizyon notunu güncelle (StockWrite geri geldi).

- [ ] **Step 5: StockWriterAgent'ı DI'a kaydet**

`Program.cs`'te CatalogWriterAgent/DiscountWriterAgent kaydının yanına aynı desende
`StockWriterAgent` kaydını ekle (kendi `McpConnection`'ı ile — Catalog/Discount örneğini birebir izle).

- [ ] **Step 6: WriteDecisionTests — StockWrite guard testi**

`tests/IngestionAgent.Tests/WriteDecisionTests.cs` içine, Catalog başarısızken StockWrite'ın
dokunmadan geçtiğini doğrulayan test ekle (Discount guard testinin muadili):
```csharp
[Fact]
public async Task StockWrite_WhenFailureAlreadySet_PassesThroughUntouched()
{
    var job = new RecordJob { Message = SampleMessage(), Failure = "CATALOG_WRITE_FAILED" };
    var executor = new StockWriteExecutor(stockAgent: null!); // guard, agent'a hiç dokunmaz
    var result = await executor.HandleAsync(job, context: null!, CancellationToken.None);
    result.Failure.ShouldBe("CATALOG_WRITE_FAILED");
}
```
(`SampleMessage()` — dosyadaki mevcut yardımcı; yoksa geçerli bir
`SupplierProductSnapshotReceived` üret.)

- [ ] **Step 7: Build + testler**

Run: `dotnet build src/agents/IngestionAgent/IngestionAgent.csproj -v q` → 0 hata
Run: `dotnet test tests/IngestionAgent.Tests/IngestionAgent.Tests.csproj` → yeşil

- [ ] **Step 8: Commit**

```bash
git add src/agents/IngestionAgent/ tests/IngestionAgent.Tests/WriteDecisionTests.cs
git commit -m "feat(ingestion): StockWrite aşaması geri geldi (feed-otoriteli supply, 3 aşama)"
```

---

### Task 6: Canlı doğrulama (manuel — Aspire)

**Files:** Yok (runtime doğrulama).

- [ ] **Step 1: Sistemi başlat** — `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- [ ] **Step 2: Feed supply günceller** — bir ürünü rezerve et (sepet), sonra feed'i tetikle;
  `Supply` feed değerine gelir, rezervasyon KORUNUR (silinmez), `Available = Supply − reserved`.
- [ ] **Step 3: Commit + feed refresh** — sipariş ver (Commit); `Available` düşük kalır; feed
  refresh sonrası `SoldInCycle` sıfırlanır, çift-sayım olmaz.
- [ ] **Step 4: Oversell penceresi** — feed peryodu (Hangfire ~30dk) ile sınırlı; oversell
  hata değil, `Available` 0'a kırpılır.

---

## Notlar

- 012 merge'inden sonra `ProductStock.cs` satır numaraları kayabilir; task'lar metot adlarıyla
  (SetReservedQuantity, Commit, AvailableAt) hedefler — numaraya güvenme.
- `SoldInCycle` yeni Marten alanı; mevcut dokümanlar default 0 ile deserialize olur (migration yok).
- `Quantity` alan adı korunduğundan `StockChangedEvent`/gRPC/Storefront kontratları değişmez.