using System;
using RPlus.SDK.Integration.Models;

#nullable enable
namespace RPlus.Kernel.Integration.Domain.Entities;

public class IntegrationRoute : RPlus.SDK.Integration.Models.IntegrationRoute
{
    private IntegrationRoute()
    {
    }

    public IntegrationRoute(
        string name,
        string routePattern,
        string targetHost,
        string targetService,
        string targetMethod,
        Guid? partnerId = null,
        int priority = 0,
        bool isActive = true)
    {
        this.Id = Guid.NewGuid();
        this.PartnerId = partnerId;
        this.Name = name;
        this.RoutePattern = routePattern;
        this.TargetHost = targetHost;
        this.TargetService = targetService;
        this.TargetMethod = targetMethod;
        this.Transport = "grpc";
        this.Priority = priority;
        this.IsActive = isActive;
        this.CreatedAt = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string pattern,
        string host,
        string service,
        string method,
        int priority,
        Guid? partnerId)
    {
        this.Name = name;
        this.RoutePattern = pattern;
        this.TargetHost = host;
        this.TargetService = service;
        this.TargetMethod = method;
        this.Priority = priority;
        this.PartnerId = partnerId;
    }

    public void Deactivate() => this.IsActive = false;

    public void Activate() => this.IsActive = true;
}
