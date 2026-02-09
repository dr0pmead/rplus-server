using System;
using System.Diagnostics.CodeAnalysis;
using RPlus.SDK.Wallet.Models;

#nullable enable
namespace RPlus.Wallet.Domain.Entities;

public class Wallet : RPlus.SDK.Wallet.Models.Wallet
{
    private Wallet()
    {
    }

    [SetsRequiredMembers]
    public Wallet(Guid userId, byte[] encryptedZeroBalance, string keyId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        BalanceEncrypted = encryptedZeroBalance;
        ReservedBalanceEncrypted = encryptedZeroBalance;
        BalanceKeyId = keyId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = 0;
    }

    public void UpdateBalance(byte[] newEncryptedBalance, string keyId)
    {
        BalanceEncrypted = newEncryptedBalance;
        BalanceKeyId = keyId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateReserved(byte[] newEncryptedReserved, string keyId)
    {
        ReservedBalanceEncrypted = newEncryptedReserved;
        BalanceKeyId = keyId;
        UpdatedAt = DateTime.UtcNow;
    }
}
