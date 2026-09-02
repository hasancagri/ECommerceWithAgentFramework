# Kontrat: Mail.Mcp (060)

Repo'nun ilk standalone MCP server'ı: `src/agents/Mail.Mcp` (ASP.NET, `ModelContextProtocol.AspNetCore`, `MapMcp("/mcp")`).
Tek tüketici: `NotificationAgent`'ın Send adımındaki minik agent (LLM tool-seçimi; imperatif `CallToolAsync` YASAK — anayasa v1.8.1).
ChatAgent'a KAYDEDİLMEZ.

## Tool: `send_mail`

| Param | Tip | Zorunlu | Not |
|---|---|---|---|
| `to` | string | evet | Alıcı e-posta |
| `subject` | string | evet | Konu (Türkçe) |
| `bodyHtml` | string | evet | HTML gövde |

- TÜM param'lar zorunlu — optional param default tuzağına girilmez (memory: mcp-tool-optional-param-default).
- Davranış: MailKit `SmtpClient` ile Mailpit'e gönderir (auth'suz, dev). Gönderen: `Smtp:From` config sabiti.
- Dönüş: kısa metin `"sent:<messageId>"`; SMTP hatasında exception → MCP error → Send agent'ı başarısızlığı worker'a taşır → `NotificationException` → Wolverine retry.

## Konfig (Options pattern)

```
SmtpOptions { Host, Port, From }   // AddOptions<SmtpOptions>().BindConfiguration(...).ValidateOnStart()
```

- Host/Port AppHost'tan Mailpit endpoint referansıyla env olarak verilir; `IConfiguration` doğrudan okunmaz.

## Aspire

- `mailpit` container: `axllent/mailpit`, SMTP 1025 + HTTP UI 8025 (`http://localhost:<port>` — canlı doğrulama arayüzü).
- `mail-mcp` projesi `launchSettings.json` İLE gelir (tuzak: yoksa Production açılır).
- `notification-agent`, `mail-mcp`'ye service discovery referansıyla bağlanır (`http://mail-mcp/mcp`).