namespace NotificationAgent;

// ChatAgent/ConstValues emsali: agent'in sabitleri tek dosyada (magic string dagitma).
public static class MailMcp
{
    // Named HttpClient + MCP transport adi (service discovery "mail-mcp" resource'unu cozer).
    public const string ClientName = "mail-mcp";
    public const string Url = "http://mail-mcp/mcp";

    // send_mail tool'unun basari sozlesmesi: donus "sent:<messageId>" ile baslar (contracts/mail-mcp.md).
    public const string SentMarker = "sent:";
}

// NotificationSent.Detail / NotificationRecord.Detail degerleri (Library izi bunlari saklar).
public static class NotificationDetails
{
    public const string Sent = "sent";
    public const string NoEmail = "no-email";
}

public static class Prompts
{
    public const string MailInstructions =
        """
        Sen bir kitapci magazasinin fiyat alarmi mail operatorusun. Sana bir tetik verilir:
        alici e-posta, urun adi, eski fiyat, yeni fiyat, urun linki.
        Gorevin iki adim: (1) kisa, samimi, TURKCE bir bildirim maili yaz; (2) send_mail aracini
        to=alici e-posta, subject=yazdigin konu, bodyHtml=yazdigin HTML govde ile cagir.
        Mail kurallari:
        - Subject kisa ve net; urun adini icersin.
        - BodyHtml gecerli basit HTML olsun (p/strong/a yeterli; style yok).
        - Govde MUTLAKA sunlari icersin: hitap, urun adi, ESKI fiyat, YENI fiyat ve
          urun linki (verilen href ile <a> etiketi).
        - Fiyatlari TL olarak ve verilen degerlerle AYNEN yaz; yuvarlama/uydurma yok.
        Aracin dondugu sonucu oldugu gibi (tek satir) yanit olarak ver; alan uydurma.
        """;
}