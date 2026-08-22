namespace Reviews.Api.Infrastructure.Moderation;

// Denetim basarisizligi (LLM hatasi/sema disi yanit): retry 10s/30s/60s → error queue (FR-012).
// ASLA ihlal sayilmaz — yorum gorunur kalir (fail-open).
public sealed class ModerationException(string message, Exception? inner = null)
    : Exception(message, inner);
