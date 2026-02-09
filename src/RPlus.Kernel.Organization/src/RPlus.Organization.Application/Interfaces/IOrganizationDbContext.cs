using Microsoft.EntityFrameworkCore;
using RPlus.Organization.Domain.Entities;

namespace RPlus.Organization.Application.Interfaces;

public interface IOrganizationDbContext
{
    DbSet<OrgNode> OrgNodes { get; }
    DbSet<Position> Positions { get; }
    DbSet<UserAssignment> UserAssignments { get; }
    DbSet<UserRoleOverride> UserRoleOverrides { get; }
    DbSet<NodeContext> NodeContexts { get; }
    DbSet<PositionContext> PositionContexts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

