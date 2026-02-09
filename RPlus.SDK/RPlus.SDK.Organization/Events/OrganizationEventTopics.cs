namespace RPlus.SDK.Organization.Events;

#nullable enable
public static class OrganizationEventTopics
{
    public const string OrganizationCreated = "kernel.organization.organization.created.v1";
    public const string OrganizationUpdated = "kernel.organization.organization.updated.v1";
    public const string OrganizationDeleted = "kernel.organization.organization.deleted.v1";
    public const string OrganizationBatchUpdated = "kernel.organization.organization.batch-updated.v1";
    public const string AssignmentChanged = "organization.assignment.changed.v1";
}
