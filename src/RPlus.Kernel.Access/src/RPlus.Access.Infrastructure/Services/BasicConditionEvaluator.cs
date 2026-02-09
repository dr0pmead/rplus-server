// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Services.BasicConditionEvaluator
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using RPlus.Access.Application.Interfaces;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Infrastructure.Services;

public class BasicConditionEvaluator : IConditionEvaluator
{
  public bool Evaluate(string? condition, Dictionary<string, object> context)
  {
    if (string.IsNullOrWhiteSpace(condition))
      return true;
    try
    {
      string[] strArray = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (strArray.Length < 3)
        return false;
      string key = strArray[0];
      string str = strArray[1];
      string s = strArray[2].Trim('\'', '"');
      object obj;
      if (!context.TryGetValue(key, out obj))
        return false;
      bool flag;
      switch (str)
      {
        case "==":
          flag = obj.ToString() == s;
          break;
        case "!=":
          flag = obj.ToString() != s;
          break;
        case ">":
          double result1;
          double result2;
          flag = double.TryParse(obj.ToString(), out result1) && double.TryParse(s, out result2) && result1 > result2;
          break;
        case "<":
          double result3;
          double result4;
          flag = double.TryParse(obj.ToString(), out result3) && double.TryParse(s, out result4) && result3 < result4;
          break;
        default:
          flag = false;
          break;
      }
      return flag;
    }
    catch
    {
      return false;
    }
  }
}
