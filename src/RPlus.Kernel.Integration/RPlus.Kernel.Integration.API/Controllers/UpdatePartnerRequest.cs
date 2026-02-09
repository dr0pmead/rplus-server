using System.Collections.Generic;

namespace RPlus.Kernel.Integration.Api.Controllers;

public sealed record UpdatePartnerRequest(
    string? Name,
    string? Description,
    bool? IsDiscountPartner,
    decimal? DiscountPartner,
    bool? IsActive,
    string? AccessLevel,
    IReadOnlyCollection<string>? ProfileFields,
    Dictionary<string, object>? Metadata);
