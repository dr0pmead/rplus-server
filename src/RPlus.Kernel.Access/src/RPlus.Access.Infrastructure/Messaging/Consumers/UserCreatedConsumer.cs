// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Messaging.Consumers.UserCreatedConsumer
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using RPlus.Core.Options;
using RPlus.SDK.Core.Messaging.Events;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Messaging.Consumers;

public class UserCreatedConsumer : KafkaConsumerBackgroundService<UserCreated>
{
  private readonly IServiceProvider _serviceProvider;

  public UserCreatedConsumer(
    IOptions<KafkaOptions> options,
    IServiceProvider serviceProvider,
    ILogger<UserCreatedConsumer> logger)
    : base(options, (ILogger) logger, "user.identity.v1", "rplus-access-service-group")
  {
    this._serviceProvider = serviceProvider;
  }

  protected override async Task HandleAsync(UserCreated message, CancellationToken ct)
  {
    UserCreatedConsumer userCreatedConsumer = this;
    IServiceScope scope = userCreatedConsumer._serviceProvider.CreateScope();
    try
    {
      IAccessDbContext dbContext = scope.ServiceProvider.GetRequiredService<IAccessDbContext>();
      bool changed = false;

      if (!await dbContext.UserAssignments.AnyAsync<LocalUserAssignment>((Expression<Func<LocalUserAssignment, bool>>) (x => x.UserId == message.UserId && x.TenantId == message.TenantId && x.RoleCode == "User"), ct))
      {
        dbContext.UserAssignments.Add(new LocalUserAssignment()
        {
          UserId = message.UserId,
          TenantId = message.TenantId,
          RoleCode = "User",
          NodeId = Guid.Empty,
          PathSnapshot = "root"
        });
        changed = true;
        userCreatedConsumer._logger.LogInformation("Assigned default 'User' role to {UserId} (Tenant: {TenantId})", (object) message.UserId, (object) message.TenantId);
      }

      bool isStaff = string.Equals(message.UserType, "Staff", StringComparison.OrdinalIgnoreCase);
      if (isStaff && !await dbContext.UserAssignments.AnyAsync<LocalUserAssignment>((Expression<Func<LocalUserAssignment, bool>>) (x => x.UserId == message.UserId && x.TenantId == message.TenantId && x.RoleCode == "Staff"), ct))
      {
        dbContext.UserAssignments.Add(new LocalUserAssignment()
        {
          UserId = message.UserId,
          TenantId = message.TenantId,
          RoleCode = "Staff",
          NodeId = Guid.Empty,
          PathSnapshot = "root"
        });
        changed = true;
        userCreatedConsumer._logger.LogInformation("Assigned 'Staff' role to {UserId} (Tenant: {TenantId})", (object) message.UserId, (object) message.TenantId);
      }

      if (changed)
      {
        var snapshots = await dbContext.EffectiveSnapshots
          .Where(x => x.UserId == message.UserId && x.TenantId == message.TenantId)
          .ToListAsync(ct);
        if (snapshots.Count > 0)
          dbContext.EffectiveSnapshots.RemoveRange(snapshots);

        int num = await dbContext.SaveChangesAsync(ct);
      }

      scope = (IServiceScope) null;
      dbContext = (IAccessDbContext) null;
    }
    finally
    {
      scope?.Dispose();
    }
  }
}
