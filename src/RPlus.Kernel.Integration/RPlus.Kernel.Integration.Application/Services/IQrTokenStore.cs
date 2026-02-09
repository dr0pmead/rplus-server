using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Application.Services;

public interface IQrTokenStore
{
    Guid? TryGetUserId(string token);
    Task InvalidateAsync(string token, string userId, CancellationToken ct);
}
