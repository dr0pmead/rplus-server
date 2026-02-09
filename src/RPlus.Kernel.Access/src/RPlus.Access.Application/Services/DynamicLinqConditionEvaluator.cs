// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.DynamicLinqConditionEvaluator
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using RPlus.Access.Application.Interfaces;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Application.Services;

public class DynamicLinqConditionEvaluator : IConditionEvaluator
{
  public bool Evaluate(string? condition, Dictionary<string, object> context)
  {
    if (string.IsNullOrWhiteSpace(condition))
      return true;
    try
    {
      return condition.Trim().ToLower() == "true" || !(condition.Trim().ToLower() == "false");
    }
    catch
    {
      return false;
    }
  }
}
