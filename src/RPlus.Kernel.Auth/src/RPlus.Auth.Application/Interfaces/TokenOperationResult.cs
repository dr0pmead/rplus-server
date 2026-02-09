// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.TokenOperationResult
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public sealed record TokenOperationResult(TokenOperationStatus Status, TokenPair? Tokens);
