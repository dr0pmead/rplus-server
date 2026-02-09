// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.ModuleDependency
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public record ModuleDependency(string ModuleId, string? MinVersion = null);
