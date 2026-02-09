using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

#nullable enable
namespace RPlus.Wallet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWalletApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        return services;
    }
}
