using System;
using System.Collections.Generic;
using RPlus.SDK.Access.Models;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class Role : RPlus.SDK.Access.Models.Role
{
    private readonly List<AccessPolicy> _policies = new List<AccessPolicy>();

    public IReadOnlyCollection<AccessPolicy> Policies
    {
        get => _policies.AsReadOnly();
    }

    public Role(Guid id, string code, string name)
    {
        this.Id = id;
        this.Code = code;
        this.Name = name;
    }

    public static Role Create(string code, string name) => new Role(Guid.NewGuid(), code, name);
}
