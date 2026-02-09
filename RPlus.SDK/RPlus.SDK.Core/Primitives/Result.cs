// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Primitives.Result
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

#nullable enable
namespace RPlus.SDK.Core.Primitives;

public class Result : Result<object?>
{
  protected Result(bool isSuccess, Error error)
    : base(null!, isSuccess, error)
  {
  }

  public static Result Success() => new Result(true, Error.None);

  public new static Result Failure(Error error) => new Result(false, error);
}
