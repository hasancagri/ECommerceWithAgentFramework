namespace Common.Exceptions;

public sealed class IngestionWriteException(string externalId, string errorCode)
    : Exception($"Ingestion write failed for '{externalId}': {errorCode}")
{
}