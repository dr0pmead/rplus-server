using System;

#nullable enable
namespace RPlus.SDK.Organization.Events;

public sealed record OrganizationLeaderBrief(Guid UserId, string Role);
