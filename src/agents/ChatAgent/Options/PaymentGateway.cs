namespace ChatAgent.Options;

// 024: uzak A2A PaymentAgent baglanti config'i — appsettings "PaymentGateway". PaymentAgentInstallmentTool
// buradan tip'li okur (config[...] magic-string yerine). A2AUrl bos ise taksit tool'u eklenmez (US2/FR-006).
public class PaymentGateway
{
    public string? A2AUrl { get; set; }
}
