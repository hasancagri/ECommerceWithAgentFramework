namespace Order.Api.Contracts.Refit.Options;

public class ClientSecretOption
{
    public required string Id { get; set; }
    public required string Secret { get; set; }
}