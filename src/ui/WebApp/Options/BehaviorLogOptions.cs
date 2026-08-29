namespace WebApp.Options;

// 048: davranış sinyali gönderiminin ayarları (042 dosya-dizini kaldırıldı; sinyaller artık
// Personalization.Api'ye HTTP gider). Enabled=false ise Enqueue sessizce yok sayılır.
public class BehaviorLogOptions
{
    public bool Enabled { get; set; } = true;

    public bool IsActive => Enabled;
}