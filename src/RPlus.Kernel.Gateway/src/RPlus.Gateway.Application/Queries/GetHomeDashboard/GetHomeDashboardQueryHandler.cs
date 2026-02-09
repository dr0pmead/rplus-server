// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Application.Queries.GetHomeDashboard.GetHomeDashboardQueryHandler
// Assembly: RPlus.Gateway.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67A55195-718A-4D21-B898-C0A623E6660E
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Application.dll

using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Gateway.Application.Contracts.Responses;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Application.Queries.GetHomeDashboard;

public class GetHomeDashboardQueryHandler : 
  IRequestHandler<GetHomeDashboardQuery, HomeDashboardResponse>
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger<GetHomeDashboardQueryHandler> _logger;

  public GetHomeDashboardQueryHandler(
    IHttpClientFactory httpClientFactory,
    ILogger<GetHomeDashboardQueryHandler> logger)
  {
    this._httpClientFactory = httpClientFactory;
    this._logger = logger;
  }

  public async Task<HomeDashboardResponse> Handle(
    GetHomeDashboardQuery request,
    CancellationToken ct)
  {
        Guid userId = request.UserId;
        Task<UserProfileDto> userProfileTask = FetchUserProfileAsync(userId, ct);
        Task<LoyaltyInfoDto> loyaltyInfoTask = FetchLoyaltyInfoAsync(userId, ct);

        await Task.WhenAll(userProfileTask, loyaltyInfoTask);

        UserProfileDto userProfile = await userProfileTask;
        LoyaltyInfoDto loyaltyInfo = await loyaltyInfoTask;

        return new HomeDashboardResponse(userProfile, loyaltyInfo);
  }

  private async Task<UserProfileDto> FetchUserProfileAsync(Guid userId, CancellationToken ct)
  {
    try
    {
      HttpResponseMessage async = await this._httpClientFactory.CreateClient("UsersService").GetAsync($"/api/v1/users/{userId}/profile", ct);
      if (!async.IsSuccessStatusCode)
      {
        this._logger.LogWarning("Failed to fetch user profile for {UserId}: {StatusCode}", (object) userId, (object) async.StatusCode);
        return new UserProfileDto(userId, "Unknown", "User", (string) null, (string) null);
      }
      UserProfileDto userProfileDto = JsonSerializer.Deserialize<UserProfileDto>(await async.Content.ReadAsStringAsync(ct), new JsonSerializerOptions()
      {
        PropertyNameCaseInsensitive = true
      });
      if ((object) userProfileDto == null)
        userProfileDto = new UserProfileDto(userId, "Unknown", "User", (string) null, (string) null);
      return userProfileDto;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error fetching user profile for {UserId}", (object) userId);
      return new UserProfileDto(userId, "Unknown", "User", (string) null, (string) null);
    }
  }

  private async Task<LoyaltyInfoDto> FetchLoyaltyInfoAsync(Guid userId, CancellationToken ct)
  {
    try
    {
      HttpResponseMessage async = await this._httpClientFactory.CreateClient("LoyaltyService").GetAsync($"/api/v1/loyalty/users/{userId}/summary", ct);
      if (!async.IsSuccessStatusCode)
      {
        this._logger.LogWarning("Failed to fetch loyalty info for {UserId}: {StatusCode}", (object) userId, (object) async.StatusCode);
        return GetHomeDashboardQueryHandler.CreateDefaultLoyalty();
      }
      LoyaltyInfoDto loyaltyInfoDto = JsonSerializer.Deserialize<LoyaltyInfoDto>(await async.Content.ReadAsStringAsync(ct), new JsonSerializerOptions()
      {
        PropertyNameCaseInsensitive = true
      });
      if ((object) loyaltyInfoDto == null)
        loyaltyInfoDto = GetHomeDashboardQueryHandler.CreateDefaultLoyalty();
      return loyaltyInfoDto;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error fetching loyalty info for {UserId}", (object) userId);
      return GetHomeDashboardQueryHandler.CreateDefaultLoyalty();
    }
  }

  private static LoyaltyInfoDto CreateDefaultLoyalty()
  {
    return new LoyaltyInfoDto(0, "Base", "base", "0%", "0%", "0%", 0, 0, 0);
  }
}
