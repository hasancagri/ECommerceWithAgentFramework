# Common Kalite/Modernizasyon Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `src/Common` kütüphanesini davranışı değiştirmeden modernize et: framework hizalama, tutarlı namespace stili, nullable doğruluğu, yanıltıcı/ölü kod temizliği.

**Architecture:** Dört bağımsız kalite ekseni (A: framework, B: namespace/stil, C: nullable, D: ölü kod). Her görev tek bir sorumluluğu değiştirir, kendi commit'ini üretir ve derleme ile doğrulanır. Test projesi olmadığından doğrulama `dotnet build` iledir.

**Tech Stack:** .NET 10, C#, Wolverine, PagedList.Core.

## Global Constraints

- Solution build komutu: `dotnet build ECommerceWithAgentFramework.slnx`
- NuGet sürümleri merkezî (`Directory.Packages.props`) — csproj'a `Version=` ekleme.
- Kod yorumları Türkçe yazılır.
- Davranış/API değişikliği YOK. Public static factory imzaları (`Ok`/`Error`/`NotFound`) aynen korunur.
- Widely-used tipler `namespace Common`'da KALIR: `FeatureResultModel`, `FeatureObjectResultModel`, `FeatureListResultModel`, `FeaturePagedResultModel`, `MessageItem`.
- Kapsam dışı: `FeatureOutputModel.cs` konsolidasyonu (E), yeni test.

---

### Task 1: Framework'ü net10.0'a hizala (A)

**Files:**
- Modify: `src/Common/Common.csproj:5`

**Interfaces:**
- Consumes: yok
- Produces: yok (yalnızca TFM değişir)

- [ ] **Step 1: TargetFramework'ü güncelle**

`src/Common/Common.csproj` içinde:

```xml
        <TargetFramework>net9.0</TargetFramework>
```

satırını şununla değiştir:

```xml
        <TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 2: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded, 0 error. (net9.0 uyarısı ortadan kalkar.)

- [ ] **Step 3: Commit**

```bash
git add src/Common/Common.csproj
git commit -m "chore(common): target net10.0 to align with solution"
```

---

### Task 2: GlobalExceptionHandler nullable düzeltmesi (C)

**Files:**
- Modify: `src/Common/Exceptions/GlobalExceptionHandler.cs:22`

**Interfaces:**
- Consumes: `FeatureResultModel` (namespace Common — değişmiyor)
- Produces: yok

- [ ] **Step 1: `= null` başlangıç atamasını kaldır**

`src/Common/Exceptions/GlobalExceptionHandler.cs` içindeki:

```csharp
        FeatureResultModel apiResultModel = null;
        switch (exception)
```

satırını şununla değiştir:

```csharp
        FeatureResultModel apiResultModel;
        switch (exception)
```

Gerekçe: `switch` default dahil her dalda `apiResultModel`'e atama yapıyor; definite-assignment sağlanır, nullable uyarısı kalkar.

- [ ] **Step 2: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded, 0 error, `apiResultModel` için CS8600 uyarısı yok.

- [ ] **Step 3: Commit**

```bash
git add src/Common/Exceptions/GlobalExceptionHandler.cs
git commit -m "fix(common): drop null init in GlobalExceptionHandler (nullable-clean)"
```

---

### Task 3: DateHelper.Now yanıltıcı parametresini kaldır (D)

**Files:**
- Modify: `src/Common/Utils/Helpers/DateHelper.cs:7-10`

**Interfaces:**
- Consumes: yok
- Produces: `DateHelper.Now()` — parametresiz. (Solution genelinde çağıranı yok; doğrulandı.)

- [ ] **Step 1: Metot imzasını sadeleştir**

`src/Common/Utils/Helpers/DateHelper.cs` içindeki:

```csharp
    public static DateTime Now(string? culture = "Europe/Istanbul")
    {
        return DateTime.UtcNow;
    }
```

bloğunu şununla değiştir:

```csharp
    // Her zaman UTC döner; eski culture parametresi yok sayılıyordu, kaldırıldı.
    public static DateTime Now()
    {
        return DateTime.UtcNow;
    }
```

- [ ] **Step 2: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded, 0 error.

- [ ] **Step 3: Commit**

```bash
git add src/Common/Utils/Helpers/DateHelper.cs
git commit -m "refactor(common): drop misleading culture param from DateHelper.Now"
```

---

### Task 4: Enumeration — nullable override + try/catch akış kontrolünü kaldır (C+D)

**Files:**
- Modify: `src/Common/Domains/Enumeration.cs`

**Interfaces:**
- Consumes: yok
- Produces: `Enumeration.FromValue<T>(int)` / `FromDisplayName<T>(string)` — davranış aynı (bulunamazsa `null`). `Equals(object?)`, `CompareTo(object?)`.

- [ ] **Step 1: Dosyanın tamamını aşağıdaki içerikle değiştir**

`src/Common/Domains/Enumeration.cs`:

```csharp
using System.Reflection;

namespace Common.Domains;

public abstract class Enumeration : IComparable
{
    public string Name { get; private set; }

    public int Id { get; private set; }

    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<T>();

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration otherValue)
        {
            return false;
        }

        var typeMatches = GetType().Equals(obj.GetType());
        var valueMatches = Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static int AbsoluteDifference(Enumeration firstValue, Enumeration secondValue)
    {
        var absoluteDifference = Math.Abs(firstValue.Id - secondValue.Id);
        return absoluteDifference;
    }

    // Eşleşme yoksa null döner; eski try/catch akış kontrolü kaldırıldı.
    public static T? FromValue<T>(int value) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(item => item.Id == value);

    public static T? FromDisplayName<T>(string displayName) where T : Enumeration =>
        GetAll<T>().FirstOrDefault(item => item.Name == displayName);

    public int CompareTo(object? obj) => Id.CompareTo(((Enumeration)obj!).Id);
}
```

Not: `Parse` yardımcısı ve `catch (Exception e)` blokları kaldırıldı; `FromValue`/`FromDisplayName` doğrudan `FirstOrDefault` döndürüyor (bulunamazsa `null` — eski davranışla aynı).

- [ ] **Step 2: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded, 0 error. `Equals`/`CompareTo` için nullable uyarısı yok.

- [ ] **Step 3: Commit**

```bash
git add src/Common/Domains/Enumeration.cs
git commit -m "refactor(common): nullable overrides + drop exception flow control in Enumeration"
```

---

### Task 5: Namespace/stil tutarlılığı (B)

**Files (hepsi Modify):**
- `src/Common/Domains/BaseModel.cs`
- `src/Common/Results/IResultModel.cs`
- `src/Common/Results/IResultObjectModel.cs`
- `src/Common/Results/IResultObjectListModel.cs`
- `src/Common/Results/IResultObjectPagedListModel.cs`
- `src/Common/Results/IResultValueModel.cs`
- `src/Common/Results/IResultPagedListModel.cs`
- `src/Common/Inputs/IInputModel.cs`
- `src/Common/Results/BaseClasses/BaseResultModel.cs`
- `src/Common/Results/BaseClasses/BaseResultObjectModel.cs`
- `src/Common/Results/BaseClasses/BaseResultObjectListModel.cs`
- `src/Common/Results/BaseClasses/BaseResultObjectPagedListModel.cs`
- `src/Common/Results/BaseClasses/BaseResultValueModel.cs`

**Interfaces:**
- Consumes: `IModel`, `IUserTrackModel` (Common.Domains), `MessageItem` (Common), `PagedList.Core.IPagedList`, `BaseInputModel` (Common.Inputs.BaseClasses).
- Produces: Arayüzler `Common.Results`, base sınıflar `Common.Results.BaseClasses`, model tabanları `Common.Domains`. `MessageItem` ve `Feature*` tipleri `namespace Common`'da kalır (değişmez).

**Namespace görünürlük mantığı (neden using eklemeye gerek yok):** `Common.Results.BaseClasses`, `Common.Results` ve `Common`'ın iç namespace'i olduğundan, enclosing-namespace araması sayesinde bu ata namespace'lerdeki tipleri (`IResult*`, `MessageItem`) `using` olmadan görür.

- [ ] **Step 1: `Domains/BaseModel.cs` — tamamını değiştir**

```csharp
namespace Common.Domains;

public abstract class BaseModel : IModel
{
    protected BaseModel()
    {
        Id = Guid.NewGuid();
        CreatedTime = DateTime.UtcNow;
        IsDeleted = false;
        IsActive = true;
    }

    public Guid Id { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }
    public DateTime? DeletedTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public abstract class BaseUserTrackModel : BaseModel, IUserTrackModel
{
    public Guid? CreatedUserId { get; set; }
    public Guid? UpdatedUserId { get; set; }
    public Guid? DeletedUserId { get; set; }
}
```

Not: Eskiden `BaseModel` `namespace Common { }` içindeydi, `BaseUserTrackModel` ise brace bloğunun DIŞINDA (global namespace) kalıyordu — ikisi de `Common.Domains`'e alındı. `IModel`/`IUserTrackModel` aynı namespace'te; gereksiz `using Common;` ve `using Common.Domains;` kaldırıldı.

- [ ] **Step 2: `Results/IResultModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results;

public interface IResultModel
{
    bool IsSuccess { get; set; }
    List<MessageItem>? Messages { get; set; }
    List<KeyValuePair<string, string>>? LocalizedMessages { get; set; }
}
```

- [ ] **Step 3: `Results/IResultObjectModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results;

public interface IResultObjectModel<TData> : IResultModel
      where TData : class, new()
{
    TData Data { get; set; }
}
```

- [ ] **Step 4: `Results/IResultObjectListModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results;

public interface IResultObjectListModel<TData> : IResultModel
      where TData : class
{
    List<TData> Data { get; set; }
}
```

- [ ] **Step 5: `Results/IResultObjectPagedListModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results;

public interface IResultObjectPagedListModel<TData> : IResultObjectListModel<TData>, IResultPagedListModel
     where TData : class, new()
{
}
```

Not: Artık kendi namespace'ine işaret eden `using Common.Results;` kaldırıldı.

- [ ] **Step 6: `Results/IResultValueModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results;

public interface IResultValueModel<TValue> : IResultModel
{
    TValue Value { get; set; }
}
```

- [ ] **Step 7: `Results/IResultPagedListModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results;

public interface IResultPagedListModel
{
    int TotalItemCount { get; set; }
    int PageCount { get; set; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
}
```

- [ ] **Step 8: `Inputs/IInputModel.cs` — tamamını değiştir**

```csharp
using Common.Inputs.BaseClasses;

namespace Common.Inputs;

public interface IInputModel
{
    string SearchText { get; set; }
}

public class InputModel : BaseInputModel
{
    public string? SearchText { get; set; }
}
```

- [ ] **Step 9: `Results/BaseClasses/BaseResultModel.cs` — tamamını değiştir**

```csharp
using System.Text.Json.Serialization;

namespace Common.Results.BaseClasses;

public abstract class BaseResultModel : IResultModel
{
    public bool IsSuccess { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<MessageItem>? Messages { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("errorMessages")] public List<KeyValuePair<string, string>>? LocalizedMessages { get; set; }

    public string GetMessage()
    {
        return Messages is null ? string.Empty : string.Join("|", Messages.Select(v => $"{v.Property} - {v.Code}"));
    }

    public string GetLocalizedMessages()
    {
        return LocalizedMessages is null ? string.Empty : string.Join("|", LocalizedMessages.Select(v => $"{v.Key} - {v.Value}"));
    }
}
```

- [ ] **Step 10: `Results/BaseClasses/BaseResultObjectModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results.BaseClasses;

public abstract class BaseResultObjectModel<TData> : BaseResultModel, IResultObjectModel<TData>
    where TData : class, new()
{
    protected BaseResultObjectModel()
    {
        Data = new TData();
    }

    public TData Data { get; set; }
}
```

Not: Kendi namespace'ini gösteren `using Common.Results.BaseClasses;` kaldırıldı; `IResultObjectModel` enclosing `Common.Results`'tan görünüyor.

- [ ] **Step 11: `Results/BaseClasses/BaseResultObjectListModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results.BaseClasses;

public abstract class BaseResultObjectListModel<TData> : BaseResultModel, IResultObjectListModel<TData>
    where TData : class
{
    protected BaseResultObjectListModel()
    {
        Data = new List<TData>();
    }
    public List<TData> Data { get; set; }
}
```

- [ ] **Step 12: `Results/BaseClasses/BaseResultObjectPagedListModel.cs` — tamamını değiştir**

```csharp
using PagedList.Core;

namespace Common.Results.BaseClasses;

public abstract class BaseResultObjectPagedListModel<TData> : BaseResultModel, IResultObjectPagedListModel<TData>
    where TData : class, new()
{
    protected BaseResultObjectPagedListModel()
    {
        IsSuccess = false;
        Data = new List<TData>();
    }

    protected BaseResultObjectPagedListModel(IPagedList metaData, List<TData> data)
    {
        Data = data;
        TotalItemCount = metaData.TotalItemCount;
        PageCount = metaData.PageCount;
        HasNextPage = metaData.HasNextPage;
        HasPreviousPage = metaData.HasPreviousPage;
        PageNumber = metaData.PageNumber;
        IsFirstPage = metaData.IsFirstPage;
        IsLastPage = metaData.IsLastPage;
    }

    protected BaseResultObjectPagedListModel(List<TData> data, int pageNumber, int pageCount, int totalItemCount)
    {
        Data = data;
        PageNumber = pageNumber;
        PageCount = pageCount;
        TotalItemCount = totalItemCount;
        HasNextPage = pageNumber < pageCount;
        HasPreviousPage = pageNumber > 1;
        IsFirstPage = pageNumber == 1;
        IsLastPage = pageNumber == pageCount;
    }

    public List<TData> Data { get; set; }
    public int TotalItemCount { get; set; }
    public int PageNumber { get; set; }
    public int PageCount { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool IsFirstPage { get; set; }
    public bool IsLastPage { get; set; }
    public bool HasNextPage { get; set; }
}
```

Not: `using Common.Results.BaseClasses;` (self) kaldırıldı; `using PagedList.Core;` korundu.

- [ ] **Step 13: `Results/BaseClasses/BaseResultValueModel.cs` — tamamını değiştir**

```csharp
namespace Common.Results.BaseClasses;

public abstract class BaseResultValueModel<TData> : BaseResultModel, IResultValueModel<TData>
{
    public TData? Value { get; set; }
}
```

Not: Gereksiz `using Common.Results;` ve `using Common.Results.BaseClasses;` kaldırıldı.

- [ ] **Step 14: Kalan brace-namespace kalmadığını doğrula**

Run:
```bash
for f in $(find src/Common -name '*.cs' -not -path '*/obj/*'); do if grep -qE "^namespace [A-Za-z0-9_.]+ *$" "$f" || grep -qE "^namespace [A-Za-z0-9_.]+\s*\{" "$f"; then echo "$f"; fi; done
```
Expected: hiçbir çıktı yok (tüm dosyalar file-scoped).

- [ ] **Step 15: Solution'ı derle (tüketiciler dahil)**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded, 0 error. Servisler `MessageItem` ve `Feature*` tiplerini `global using Common;` üzerinden görmeye devam eder.

- [ ] **Step 16: Commit**

```bash
git add src/Common/Domains/BaseModel.cs \
  src/Common/Results/IResultModel.cs \
  src/Common/Results/IResultObjectModel.cs \
  src/Common/Results/IResultObjectListModel.cs \
  src/Common/Results/IResultObjectPagedListModel.cs \
  src/Common/Results/IResultValueModel.cs \
  src/Common/Results/IResultPagedListModel.cs \
  src/Common/Inputs/IInputModel.cs \
  src/Common/Results/BaseClasses/BaseResultModel.cs \
  src/Common/Results/BaseClasses/BaseResultObjectModel.cs \
  src/Common/Results/BaseClasses/BaseResultObjectListModel.cs \
  src/Common/Results/BaseClasses/BaseResultObjectPagedListModel.cs \
  src/Common/Results/BaseClasses/BaseResultValueModel.cs
git commit -m "refactor(common): file-scoped + folder-aligned namespaces"
```

---

## Notlar

- Görev sırası bağımsızdır; ancak Task 5 en geniş yüzey olduğu için en sona konmuştur.
- Her görev tek başına derlenebilir ve geri alınabilir bir commit üretir.
- `MessageItem` ve `Feature*` tipleri bilinçli olarak `namespace Common`'da bırakıldı (tüketici churn'ünü önlemek için).