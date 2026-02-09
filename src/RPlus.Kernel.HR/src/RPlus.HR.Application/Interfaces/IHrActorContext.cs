namespace RPlus.HR.Application.Interfaces;

public interface IHrActorContext
{
    Guid? ActorUserId { get; }
    string ActorType { get; }
    string? ActorService { get; }
}

