using System;
using System.Collections.Generic;

namespace RPlus.SDK.Contracts.Events;

public record UserUpdated(
    string UserId,
    Dictionary<string, string> ChangedFields,
    DateTime UpdatedAt);
