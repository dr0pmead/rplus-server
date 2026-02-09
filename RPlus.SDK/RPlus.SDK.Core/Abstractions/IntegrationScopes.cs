// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.IntegrationScopes
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public static class IntegrationScopes
{
  public const string SystemPing = "external.system.ping";
  public const string ProxyInvoke = "external.proxy.invoke";
  public const string UsersGetByQr = "external.users.get_by_qr";
  public const string UsersRead = "external.users.read";
  public const string TransactionsCreate = "external.transactions.create";
  public const string LoyaltyBalanceRead = "external.loyalty.balance.read";
}
