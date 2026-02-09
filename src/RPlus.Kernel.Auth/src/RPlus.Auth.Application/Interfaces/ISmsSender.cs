// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.ISmsSender
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public interface ISmsSender
{
  Task<SmsSendResult> SendAsync(
    string phoneE164,
    string message,
    CancellationToken cancellationToken);
}
