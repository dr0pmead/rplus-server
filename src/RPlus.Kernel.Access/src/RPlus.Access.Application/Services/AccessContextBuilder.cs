// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Services.AccessContextBuilder
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using Microsoft.EntityFrameworkCore;
using RPlus.Access.Application.Interfaces;
using RPlus.SDK.Access.Models;
using RPlus.Access.Domain.Entities;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;

#nullable enable
namespace RPlus.Access.Application.Services;

public class AccessContextBuilder : IAccessContextBuilder
{
  private readonly IAccessDbContext _db;
  private readonly IOrganizationClient _orgClient;

  public AccessContextBuilder(IAccessDbContext db, IOrganizationClient orgClient)
  {
    this._db = db;
    this._orgClient = orgClient;
  }

  public async Task<RPlus.SDK.Access.Models.AccessContext> BuildContextAsync(
    Guid userId,
    string? action,
    Guid? nodeId,
    Dictionary<string, object>? rawContext,
    CancellationToken ct = default (CancellationToken))
  {
    Dictionary<string, object> raw = rawContext ?? new Dictionary<string, object>();
    Guid tenantId = Guid.Empty;
    object obj1;
    Guid result1;
    if (raw.TryGetValue("TenantId", out obj1) && Guid.TryParse(obj1.ToString(), out result1))
      tenantId = result1;
    HashSet<string> hashSet = (await this._db.UserAssignments.Where<LocalUserAssignment>((Expression<Func<LocalUserAssignment, bool>>) (x => x.UserId == userId)).ToListAsync<LocalUserAssignment>(ct)).Select<LocalUserAssignment, string>((Func<LocalUserAssignment, string>) (x => x.RoleCode)).ToHashSet<string>();
    string str1 = raw.ContainsKey("IpAddress") ? raw["IpAddress"].ToString() ?? "" : "";
    string str2 = raw.ContainsKey("UserAgent") ? raw["UserAgent"].ToString() ?? "" : "";
    int num = 1;
    object obj2;
    int result2;
    if (raw.TryGetValue("aal", out obj2) && int.TryParse(obj2.ToString(), out result2))
      num = result2;
    List<string> stringList = new List<string>();
    object obj3;
    if (raw.TryGetValue("amr", out obj3))
    {
      switch (obj3)
      {
        case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Array:
          stringList = jsonElement.EnumerateArray().Select<JsonElement, string>((Func<JsonElement, string>) (x => x.ToString())).ToList<string>();
          break;
        case IEnumerable<string> source:
          stringList = source.ToList<string>();
          break;
        case string str3:
          stringList.Add(str3);
          break;
      }
    }
    DateTime? nullable = new DateTime?();
    object obj4;
    long result3;
    if (raw.TryGetValue("auth_time", out obj4) && long.TryParse(obj4.ToString(), out result3))
      nullable = new DateTime?(DateTimeOffset.FromUnixTimeSeconds(result3).UtcDateTime);
    string str4 = raw.ContainsKey("device_id") ? raw["device_id"].ToString() : (string) null;
    AccessContext accessContext = new AccessContext()
    {
      Identity = new IdentityContext()
      {
        UserId = userId,
        TenantId = tenantId,
        EffectiveRoles = hashSet
      },
      Authentication = new AuthenticationContext()
      {
        Aal = num,
        Amr = stringList,
        AuthTime = nullable,
        DeviceId = str4
      },
      Resource = new ResourceContext() { NodeId = nodeId },
      Request = new RequestContext()
      {
        IpAddress = str1,
        UserAgent = str2
      },
      Attributes = raw
    };
    raw = (Dictionary<string, object>) null;
    return accessContext;
  }
}
