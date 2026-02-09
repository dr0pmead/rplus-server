using RPlus.Auth.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Infrastructure.Services;

public class SmsOtpDeliveryService : IOtpDeliveryService
{
    public Task<bool> DeliverAsync(string phone, string code, string? channel, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}
