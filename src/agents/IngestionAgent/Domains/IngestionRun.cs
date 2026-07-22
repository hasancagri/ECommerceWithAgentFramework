namespace IngestionAgent.Domains;

public enum IngestionRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2
}

public enum FetchStatus
{
    Fetched = 0,
    Unreachable = 1,
    Empty = 2
}

// Bir tetiklemenin sonucu: sayaçlar + genel durum (FR-022). Yaşam döngüsü metotlarla yönetilir:
// Start → Complete/Fail. Failed run yalnız beklenmeyen çökmede; erişilemeyen feed run'ı düşürmez.
public class IngestionRun
{
    public Guid Id { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public IngestionRunStatus Status { get; private set; }
    public List<SupplierRunResult> Suppliers { get; private set; } = [];

    private IngestionRun() { } // Marten/Newtonsoft

    public static IngestionRun Start() => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow,
        Status = IngestionRunStatus.Running
    };

    public void Complete(SupplierRunResult result)
    {
        Status = IngestionRunStatus.Completed;
        FinishedAtUtc = DateTime.UtcNow;
        Suppliers = [result];
    }

    public void Fail()
    {
        Status = IngestionRunStatus.Failed;
        FinishedAtUtc = DateTime.UtcNow;
    }
}

// Run içi sayaçlar: servis run boyunca biriktirir, kapanışta run'a yazılır.
public class SupplierRunResult
{
    public string SupplierCode { get; set; } = default!;
    public FetchStatus FetchStatus { get; set; }
    public int New { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}