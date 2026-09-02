namespace NotificationAgent;

// Mail GONDERIM basarisizligi (SMTP/MCP/tool-secimi): retry 10s/30s/60s → error queue (FR-008).
// Compose LLM hatasi bu SINIFA GIRMEZ — yedek sablonla gonderime devam edilir (spec Assumption).
public sealed class NotificationException(string message, Exception? inner = null)
    : Exception(message, inner);