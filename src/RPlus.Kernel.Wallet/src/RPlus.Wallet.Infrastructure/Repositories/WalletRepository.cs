using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RPlus.Wallet.Domain.Exceptions;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Persistence;
using WalletEntity = RPlus.Wallet.Domain.Entities.Wallet;
using WalletTransactionEntity = RPlus.Wallet.Domain.Entities.WalletTransaction;

#nullable enable
namespace RPlus.Wallet.Infrastructure.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly WalletDbContext _dbContext;
    private IDbContextTransaction? _currentTransaction;

    public WalletRepository(WalletDbContext dbContext) => _dbContext = dbContext;

    public Task<WalletEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _dbContext.Wallets.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public Task<WalletEntity?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken ct = default)
    {
        var provider = _dbContext.Database.ProviderName;
        if (!string.IsNullOrEmpty(provider) && provider.Contains("InMemory"))
        {
            return _dbContext.Wallets.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        }

        return _dbContext.Wallets
            .FromSqlInterpolated($"SELECT * FROM wallets WHERE \"UserId\" = {userId} FOR UPDATE")
            .FirstOrDefaultAsync(ct);
    }

    public Task AddAsync(WalletEntity wallet, CancellationToken ct = default) =>
        _dbContext.Wallets.AddAsync(wallet, ct).AsTask();

    public Task AddTransactionAsync(WalletTransactionEntity transaction, CancellationToken ct = default) =>
        _dbContext.WalletTransactions.AddAsync(transaction, ct).AsTask();

    public Task<bool> ExistsOperationAsync(string operationId, CancellationToken ct = default) =>
        _dbContext.WalletTransactions.AnyAsync(t => t.OperationId == operationId, ct);

    public Task<WalletTransactionEntity?> GetTransactionByOperationIdAsync(string operationId, CancellationToken ct = default) =>
        _dbContext.WalletTransactions.FirstOrDefaultAsync(t => t.OperationId == operationId, ct);

    public Task UpdateAsync(WalletEntity wallet, CancellationToken ct = default)
    {
        if (_dbContext.Entry(wallet).State != EntityState.Added)
        {
            _dbContext.Wallets.Update(wallet);
        }

        return Task.CompletedTask;
    }

    public async Task<List<WalletTransactionEntity>> GetTransactionsAsync(
        Guid userId,
        int limit,
        string? cursor,
        string? source,
        CancellationToken ct = default)
    {
        IQueryable<WalletTransactionEntity> query = _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (!string.IsNullOrEmpty(source))
        {
            query = query.Where(t => t.Source == source);
        }

        if (!string.IsNullOrEmpty(cursor) && long.TryParse(cursor, out var ticks))
        {
            var dateCursor = new DateTime(ticks, DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAt < dateCursor);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task ConvertToTransactionAsync(Func<Task> action, CancellationToken ct = default) => action();

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        await _currentTransaction.CommitAsync(ct);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        await _currentTransaction.RollbackAsync(ct);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public async Task<List<WalletTransactionEntity>> GetMonthlyTransactionsAsync(
        Guid userId, 
        int year, 
        int month, 
        string[]? sourceTypes, 
        CancellationToken ct = default)
    {
        IQueryable<WalletTransactionEntity> query = _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Year == year && t.Month == month && t.Status == "Completed");

        if (sourceTypes != null && sourceTypes.Length > 0)
        {
            query = query.Where(t => t.SourceType != null && sourceTypes.Contains(t.SourceType));
        }

        return await query.ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Optimistic concurrency failure");
        }
    }
}
