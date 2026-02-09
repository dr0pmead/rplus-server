// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Exceptions.OrganizationUnavailableException
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;

#nullable enable
namespace RPlus.Access.Domain.Exceptions;

public class OrganizationUnavailableException : Exception
{
  public OrganizationUnavailableException(string message)
    : base(message)
  {
  }

  public OrganizationUnavailableException(string message, Exception innerException)
    : base(message, innerException)
  {
  }
}
