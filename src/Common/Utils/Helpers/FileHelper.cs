namespace Common.Utils.Helpers;
public static class FileHelper
{
    private static readonly HashSet<string> AllowedExtensions = new HashSet<string> { ".xlsx", ".xls" };

    public static bool IsExcelFile(string fileName)
    {
        var fileExt = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.Contains(fileExt);
    }
}
