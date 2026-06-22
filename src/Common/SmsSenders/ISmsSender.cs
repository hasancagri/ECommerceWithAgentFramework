namespace Common.SmsSenders;

public interface ISmsSender
{
    Task<ISmsOutput> SendAsync(ISmsInput input);
}
public interface ISmsInput
{
    public Guid TraceId { get; set; }
    public string Phone { get; set; }
    public string Message { get; set; }
}
public interface ISmsOutput
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }
}
