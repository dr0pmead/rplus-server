namespace RPlus.Documents.Api.Authorization;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowSelfAttribute : Attribute
{
    public AllowSelfAttribute(string routeUserIdParameterName = "userId")
    {
        RouteUserIdParameterName = routeUserIdParameterName;
    }

    public string RouteUserIdParameterName { get; }
}
