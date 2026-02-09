using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletEntity = RPlus.Wallet.Domain.Entities.Wallet;
using WalletTransactionEntity = RPlus.Wallet.Domain.Entities.WalletTransaction;

#nullable enable
namespace RPlus.Wallet.Domain.Repositories;

public interface IWalletRepository
{
    Task<WalletEntity?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<WalletEntity?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(WalletEntity wallet, CancellationToken ct = default);
    Task AddTransactionAsync(WalletTransactionEntity transaction, CancellationToken ct = default);
    Task<bool> ExistsOperationAsync(string operationId, CancellationToken ct = default);
    Task<WalletTransactionEntity?> GetTransactionByOperationIdAsync(string operationId, CancellationToken ct = default);
    Task UpdateAsync(WalletEntity wallet, CancellationToken ct = default);
    Task<List<WalletTransactionEntity>> GetTransactionsAsync(Guid userId, int limit, string? cursor, string? source, CancellationToken ct = default);
    Task<List<WalletTransactionEntity>> GetMonthlyTransactionsAsync(Guid userId, int year, int month, string[]? sourceTypes, CancellationToken ct = default);
    Task ConvertToTransactionAsync(Func<Task> action, CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

