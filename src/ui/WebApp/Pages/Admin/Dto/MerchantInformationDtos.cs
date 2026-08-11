namespace WebApp.Pages.Admin.Dto;

// 033: DropShop merchant kimliği — admin ekranından Customer.Api'ye yazılır/okunur.
public record SetMerchantInformationRequest(Guid MerchantId, string MerchantKey);

// Durum görünümü: key ASLA taşınmaz (FR-004); yalnız varlık + merchantId + güncelleme zamanı.
public record MerchantInformationStatusDto(Guid MerchantId, DateTime? UpdatedTime);
