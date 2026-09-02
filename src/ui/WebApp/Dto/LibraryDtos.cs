namespace WebApp.Dto;

// 060: Library API sözleşmesi (contracts/library-api.md) — yalnız ihtiyaç duyulan alanlar.

// Detay sayfası düğme durumu.
public record PriceAlarmStatusDto(bool Exists);

// Alarm kurma gövdesi; Email WebApp cookie claim'inden dolar (R3 snapshot — kullanıcıdan alınmaz).
public record CreatePriceAlarmRequestDto(Guid ProductId, string ProductName, decimal CurrentPrice, string Email);