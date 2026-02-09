using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RPlus.SDK.Auth.Models;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

[Table("auth_credentials")]
public class AuthCredentialEntity : AuthCredential
{
    [Key]
    [Column("user_id")]
    public new Guid UserId 
    { 
        get => base.UserId; 
        set => base.UserId = value; 
    }

    [Column("password_hash")]
    public new byte[] PasswordHash 
    { 
        get => base.PasswordHash; 
        set => base.PasswordHash = value; 
    }

    [Column("password_salt")]
    public new byte[] PasswordSalt 
    { 
        get => base.PasswordSalt; 
        set => base.PasswordSalt = value; 
    }

    [Column("changed_at")]
    public new DateTime ChangedAt 
    { 
        get => base.ChangedAt; 
        set => base.ChangedAt = value; 
    }

    [Column("created_at")]
    public new DateTime CreatedAt 
    { 
        get => base.CreatedAt; 
        set => base.CreatedAt = value; 
    }
}
