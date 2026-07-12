using System.Text.RegularExpressions;

namespace Common.Utils.Helpers;

public static class Base64Helper
{
    public static bool IsBase64String(string base64)
    {
        base64 = base64.Trim();
        return base64.Length % 4 == 0 && Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }

    public static async Task<string> ConvertFromUrl(string url)
    {
        using HttpClient client = new HttpClient();
    
        var svgBytes = await client.GetByteArrayAsync(url);
        
        var base64String = Convert.ToBase64String(svgBytes);

        return base64String;
    }
}
