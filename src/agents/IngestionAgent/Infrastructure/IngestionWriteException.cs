namespace IngestionAgent.Infrastructure;

// Result → exception sınır köprüsü (plan Complexity Tracking): Wolverine retry/DLQ politikası
// exception-tabanlıdır. Mesaj DLQ'ya bu bağlamla düşer → kayıt kimliği + hata kodu görünür (FR-020).
public sealed class IngestionWriteException(string externalId, string errorCode)
    : Exception($"Ingestion write failed for '{externalId}': {errorCode}")
{
    public string ExternalId { get; } = externalId;
    public string ErrorCode { get; } = errorCode;
}