using System.ComponentModel.DataAnnotations;

namespace Procurement.Api.Options;

// Feed çekim zamanlaması (Options pattern — tüketici düz POCO enjekte eder, OptionsExt unwrap'ler).
public class FeedPullOptions
{
    [Required]
    public string PullCron { get; set; } = "*/30 * * * *";

    [Range(0, 3600)]
    public int FirstPullDelaySeconds { get; set; } = 60;
}