using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Application;

namespace RPlus.Kernel.Integration.Application.Features.Partners.Queries;

public sealed record ListPartnersQuery(int Page, int PageSize, string? Search) : IRequest<ListPartnersResult>, IBaseRequest;

public sealed class ListPartnersQueryHandler : IRequestHandler<ListPartnersQuery, ListPartnersResult>
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;
    private readonly IIntegrationDbContext _dbContext;

    public ListPartnersQueryHandler(IIntegrationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ListPartnersResult> Handle(ListPartnersQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize <= 0 ? DefaultPageSize : request.PageSize, 1, MaxPageSize);

        var query = _dbContext.Partners.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new ListPartnersResult(items, total);
    }
}
