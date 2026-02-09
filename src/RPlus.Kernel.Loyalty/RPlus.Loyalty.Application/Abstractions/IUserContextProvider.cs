using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Application.Abstractions;

public interface IUserContextProvider
{
    Task<UserContext?> GetAsync(Guid userId, DateTime asOfUtc, CancellationToken ct);
}

