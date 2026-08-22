namespace ChatAgent.Options;

// OpenAI sohbet istemcisi config'i (appsettings "OpenAI"). ApiKey zorunlu (ValidateOnStart fail-fast);
// Model verilmezse gpt-4o-mini. IChatClient bu POCO'dan DI factory ile kurulur (config[...] yerine).
public class OpenAiOption
{
    [System.ComponentModel.DataAnnotations.Required]
    public string ApiKey { get; set; } = default!;

    public string Model { get; set; } = "gpt-4o-mini";
}
