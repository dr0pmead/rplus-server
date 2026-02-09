namespace RPlus.SDK.Integration.Contracts;

public class ExternalResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public Guid CorrelationId { get; set; }

    public static ExternalResult<T> Ok(T data, Guid correlationId) => new() 
    { 
        Success = true, 
        Data = data, 
        CorrelationId = correlationId 
    };

    public static ExternalResult<T> Fail(string errorCode, string message, Guid correlationId) => new() 
    { 
        Success = false, 
        ErrorCode = errorCode, 
        Message = message, 
        CorrelationId = correlationId 
    };
}
