// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Clients.HttpOrganizationClient
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Clients;

public class HttpOrganizationClient : IOrganizationClient
{
  private readonly HttpClient _httpClient;

  public HttpOrganizationClient(HttpClient httpClient) => this._httpClient = httpClient;

  public async Task<List<LocalUserAssignment>> GetUserAssignmentsAsync(
    Guid userId,
    CancellationToken cancellationToken)
  {
    List<LocalUserAssignment> assignmentsAsync;
    try
    {
      assignmentsAsync = await this._httpClient.GetFromJsonAsync<List<LocalUserAssignment>>($"api/organization/users/{userId}/assignments", cancellationToken) ?? new List<LocalUserAssignment>();
    }
    catch (Exception ex)
    {
      throw new Exception("Organization Service Unavailable for Strong Consistency Check");
    }
    return assignmentsAsync;
  }
}
