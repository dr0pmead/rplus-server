// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Runtime.ProjectionRuntime
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using Microsoft.Extensions.Logging;

#nullable enable
namespace RPlus.SDK.Infrastructure.Runtime;

public class ProjectionRuntime
{
  private readonly ILogger<ProjectionRuntime> _logger;

  public ProjectionRuntime(ILogger<ProjectionRuntime> logger) => this._logger = logger;
}
