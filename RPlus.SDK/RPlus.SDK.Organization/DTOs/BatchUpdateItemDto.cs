using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Organization.DTOs;

public sealed record BatchUpdateItemDto(Guid Id, Dictionary<string, object> ChangedFields);
