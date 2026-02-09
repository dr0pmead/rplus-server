// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Api.Services.IUserProfileProvider
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 8ABF1D32-8F85-446A-8A49-54981F839476
// Assembly location: F:\RPlus Framework\Recovery\organization\ExecuteService.dll

using RPlus.Organization.Api.Contracts;
using UserProfileDto = RPlus.Organization.Api.Contracts.OrganizationUserProfileDto;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Organization.Api.Services;

public interface IUserProfileProvider
{
  Task<IReadOnlyDictionary<Guid, OrganizationUserProfileDto>> GetProfilesAsync(
    IEnumerable<Guid> userIds,
    CancellationToken cancellationToken);
}
