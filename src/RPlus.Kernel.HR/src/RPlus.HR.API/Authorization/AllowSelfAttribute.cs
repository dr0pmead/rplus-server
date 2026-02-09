namespace RPlus.HR.Api.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AllowSelfAttribute : Attribute
{
    public AllowSelfAttribute(string routeUserIdParameterName = "userId")
    {
        RouteUserIdParameterName = routeUserIdParameterName;
    }

    public string RouteUserIdParameterName { get; }
}

