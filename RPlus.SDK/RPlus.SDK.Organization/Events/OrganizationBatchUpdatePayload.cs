using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Organization.Events;

public sealed record OrganizationBatchUpdatePayload(
    DateTime OccurredAt,
    List<OrganizationBatchUpdateItem> Updates);
