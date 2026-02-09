// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.ISystemTransactionSignatureService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using System;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public interface ISystemTransactionSignatureService
{
  string CreateSignature(
    Guid userId,
    TransactionType type,
    int points,
    string operationId,
    DateTime? expiresAt,
    DateTime timestamp);
}
