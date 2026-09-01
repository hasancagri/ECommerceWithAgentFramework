namespace WebApp.Options;

// PostHog tarayıcı-taraflı analytics (JS snippet layout'ta). ApiKey public-safe
// (write-only ingest); user-secrets'ten gelir. Boşsa snippet basılmaz (analytics
// yoklugu uygulamayı çökertmez — fail-fast YOK).
public class PostHogOption
{
    public string? ApiKey { get; set; }
    public string Host { get; set; } = "https://eu.i.posthog.com";
}