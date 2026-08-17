namespace ChatAgent.ExternalAgents;

// 024/038: uzak A2A PaymentAgent'i (ayri solution) assistant'a ODEME araci olarak baglar.
// 038: emeklilik geri alindi — taksit sorgusu (quote-installments, vault token) + kayitli kartla
// cekim (charge_saved_card) artik BU yoldan yurur (Customer.Api MCP odeme tool'lari sokuldu).
// Kontrat-once: uzak taraf yoksa/erisilemezse fail-open -> null doner, assistant bu arac olmadan
// acilir (taksit/odeme "su an yapilamiyor"). Cagri: A2ACardResolver(url, httpClient) ->
// card.Skills dogrula -> GetAIAgentAsync() -> AsAIFunction() (tek NL InvokeAgentAsync; uzak agent
// LLM router'i MCP tool sirasini kurar).
public static class PaymentAgentInstallmentTool
{
    public static async Task<AITool?> TryBuildAsync(
        PaymentGateway paymentGateway, IHttpClientFactory httpClientFactory, ILogger logger)
    {
        var url = paymentGateway.A2AUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation("A2A PaymentAgent url yok -> odeme araci eklenmedi (graceful-degrade).");
            return null;
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient(A2APayment.HttpClient);
            var resolver = new A2ACardResolver(new Uri(url), httpClient: httpClient);

            // Yetenek dogrulamasi: 038 canli akis skill'leri (vault-token taksit + cekim) kartta olmali.
            AgentCard card = await resolver.GetAgentCardAsync();
            bool HasSkill(string id) => card.Skills?.Any(s =>
                string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)) ?? false;

            if (!HasSkill(A2APayment.QuoteInstallmentsSkill) || !HasSkill(A2APayment.ChargeSkill))
            {
                logger.LogWarning(
                    "A2A PaymentAgent card'inda '{Quote}'/'{Charge}' skill'leri eksik -> odeme araci eklenmedi.",
                    A2APayment.QuoteInstallmentsSkill, A2APayment.ChargeSkill);
                return null;
            }

            var remote = await resolver.GetAIAgentAsync();
            logger.LogInformation("A2A PaymentAgent baglandi ({Url}) -> odeme araci eklendi.", url);
            return remote.AsAIFunction();
        }
        catch (Exception ex)
        {
            // Fail-open: uzak taraf erisilemez -> boot cokmez, arac yok, diger yetenekler calisir.
            logger.LogWarning(ex, "A2A PaymentAgent'e baglanilamadi ({Url}) -> odeme araci eklenmedi.", url);
            return null;
        }
    }
}