namespace WebApp.Options;

// 042: davranış JSONL yazıcısının ayarları. Directory'yi AppHost enjekte eder
// (BehaviorLog__Directory); boşsa yazıcı sessizce devre dışı kalır (FR-008 — sayfa akışı asla etkilenmez).
public class BehaviorLogOptions
{
    public string Directory { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public bool IsActive => Enabled && !string.IsNullOrWhiteSpace(Directory);
}