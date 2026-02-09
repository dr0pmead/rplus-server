// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Primitives.Result`1
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Primitives;

public class Result<TValue>
{
  public TValue? Value { get; }

  public Error Error { get; }

  public bool IsSuccess { get; }

  public bool IsFailure => !this.IsSuccess;

  protected Result(TValue? value, bool isSuccess, Error error)
  {
    if (isSuccess && error != Error.None && error != Error.NullValue)
      throw new InvalidOperationException();
    if (!isSuccess && (error == Error.None || error == Error.NullValue))
      throw new InvalidOperationException();
    this.Value = value;
    this.IsSuccess = isSuccess;
    this.Error = error;
  }

  public static Result<TValue> Success(TValue value) => new Result<TValue>(value, true, Error.None);

  public static Result<TValue> Failure(Error error)
  {
    return new Result<TValue>(default (TValue), false, error);
  }

  public static implicit operator Result<TValue>(TValue value) => Result<TValue>.Success(value);

  public static implicit operator Result<TValue>(Error error) => Result<TValue>.Failure(error);
}
