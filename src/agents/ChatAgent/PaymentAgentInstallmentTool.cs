namespace ChatAgent;

// 024: uzak A2A PaymentAgent'i (ayri solution) assistant'a taksit-sorgu tool'u olarak baglar.
// Kontrat-once: uzak taraf yoksa/erisilemezse fail-open -> null doner, assistant tool'suz acilir
// (US2/FR-006). Cagri: A2ACardResolver(url, httpClient) -> card.Skills'te installment_quote
// dogrula -> GetAIAgentAsync() -> AsAIFunction() (tek NL InvokeAgentAsync; uzak agent skill'e yonlendirir).
public static class PaymentAgentInstallmentTool
{
    public static async Task<AITool?> TryBuildAsync(
        IConfiguration config, IHttpClientFactory httpClientFactory, ILogger logger)
    {
        var url = config[A2APayment.A2AUrlConfigKey];
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation("A2A PaymentAgent url yok -> taksit tool'u eklenmedi (graceful-degrade).");
            return null;
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient(A2APayment.HttpClient);
            var resolver = new A2ACardResolver(new Uri(url), httpClient: httpClient);

            // Yetenek dogrulamasi: kartta installment_quote skill'i yoksa tool ekleme (US2).
            AgentCard card = await resolver.GetAgentCardAsync();
            var hasSkill = card.Skills?.Any(s =>
                string.Equals(s.Id, A2APayment.InstallmentQuoteSkill, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasSkill)
            {
                logger.LogWarning("A2A PaymentAgent card'inda '{Skill}' skill'i yok -> taksit tool'u eklenmedi.",
                    A2APayment.InstallmentQuoteSkill);
                return null;
            }

            AIAgent remote = await resolver.GetAIAgentAsync();
            logger.LogInformation("A2A PaymentAgent baglandi ({Url}) -> taksit tool'u eklendi.", url);
            return remote.AsAIFunction();
        }
        catch (Exception ex)
        {
            // Fail-open: uzak taraf erisilemez -> boot cokmez, tool yok, diger yetenekler calisir.
            logger.LogWarning(ex, "A2A PaymentAgent'e baglanilamadi ({Url}) -> taksit tool'u eklenmedi.", url);
            return null;
        }
    }
}