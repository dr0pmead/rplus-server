// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.Monitoring.UserMetrics
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using Prometheus;
using RPlus.Users.Application.Interfaces.Monitoring;

#nullable enable
namespace RPlus.Users.Infrastructure.Monitoring;

public sealed class UserMetrics : IUserMetrics
{
  private static readonly Counter UsersCreatedTotal = Metrics.CreateCounter("users_created_total", "Total number of users created");
  private static readonly Counter UserStatusChangesTotal;

  public void IncUserCreated() => UserMetrics.UsersCreatedTotal.Inc(1.0);

  public void IncStatusChanged(string status)
  {
    UserMetrics.UserStatusChangesTotal.WithLabels(new string[1]
    {
      status
    }).Inc(1.0);
  }

  static UserMetrics()
  {
    CounterConfiguration configuration = new CounterConfiguration();
    configuration.LabelNames = new string[1]{ "status" };
    UserMetrics.UserStatusChangesTotal = Metrics.CreateCounter("users_status_changed_total", "Total number of user status changes", configuration);
  }
}
