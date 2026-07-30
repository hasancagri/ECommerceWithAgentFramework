using System.Text.Json;
using Refit;

namespace WebApp.Extensions;

public static class ProblemDetailsExtensions
{
    // Extension method for logging ProblemDetails
    public static void LogProblemDetails(this ILogger logger, ApiException? apiException)
    {
        if (string.IsNullOrEmpty(apiException!.Content))
        {
            logger.LogError("API error {Status} {Uri}: {Message}",
                (int)apiException.StatusCode, apiException.Uri, apiException.Message);
            return;
        }

        // API RFC7807 degil, FeatureResultModel zarfi dondurur; gercek HTTP status + ham govde logla.
        logger.LogError("API error {Status} {Uri}: {Content}",
            (int)apiException.StatusCode, apiException.Uri, apiException.Content);
    }
}