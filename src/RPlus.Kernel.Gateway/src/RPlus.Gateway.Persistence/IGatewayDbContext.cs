using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RPlus.Gateway.Domain.Entities;

namespace RPlus.Gateway.Persistence;

public interface IGatewayDbContext
{
    DbSet<GatewayRoute> Routes { get; }
    DbSet<GatewayCluster> Clusters { get; }
    DbSet<AppRelease> AppReleases { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
