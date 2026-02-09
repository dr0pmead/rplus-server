// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Contracts.External.ExternalResult`1
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

using RPlus.SDK.Core.Errors;

#nullable enable
namespace RPlus.SDK.Contracts.External;

public record ExternalResult<T>
{
  public bool Success { get; init; }

  public T? Data { get; init; }

  public ErrorCategory ErrorCode { get; init; }

  public string? Message { get; init; }

  public string CorrelationId { get; init; } = string.Empty;

  public static ExternalResult<T> Ok(T data, string correlationId)
  {
    return new ExternalResult<T>()
    {
      Success = true,
      Data = data,
      ErrorCode = ErrorCategory.None,
      CorrelationId = correlationId
    };
  }

  public static ExternalResult<T> Fail(
    ErrorCategory errorCode,
    string correlationId,
    string? message = null)
  {
    return new ExternalResult<T>()
    {
      Success = false,
      ErrorCode = errorCode,
      Message = message,
      CorrelationId = correlationId
    };
  }

  public ExternalResult()
  {
  }
}
