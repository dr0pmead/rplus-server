namespace RPlus.HR.Api.Events;

public sealed record HrCustomFieldFilledEvent(
    Guid UserId,
    string FieldKey,
    bool Required,
    string Entity,
    Guid? ActorUserId,
    string ActorType,
    string? ActorService,
    DateTime FilledAtUtc)
{
    public const string EventName = "hr.custom_field.filled.v1";
}
