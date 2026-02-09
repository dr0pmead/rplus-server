// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Interfaces.StepUpChallenge
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using System;

#nullable enable
namespace RPlus.Access.Application.Interfaces;

public record StepUpChallenge(int RequiredLevel, string Reason, TimeSpan? MaxAge);
