using MediatR;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Events;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.SDK.Eventing.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Application.Features.Partners.Commands;

public sealed class CreatePartnerCommandHandler : IRequestHandler<CreatePartnerCommand, Guid>
{
    private readonly IIntegrationDbContext _db;
    private readonly IEventPublisher _events;

    public CreatePartnerCommandHandler(IIntegrationDbContext db, IEventPublisher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<Guid> Handle(CreatePartnerCommand request, CancellationToken cancellationToken)
    {
        var partner = new IntegrationPartner(
            request.Name,
            request.Description ?? string.Empty,
            request.IsDiscountPartner,
            request.DiscountPartner,
            request.AccessLevel);
        _db.Partners.Add(partner);
        await _db.SaveChangesAsync(cancellationToken);

        var evt = new IntegrationPartnerCreatedEvent(
            partner.Id,
            partner.Name,
            partner.AccessLevel,
            partner.IsDiscountPartner,
            partner.CreatedAt);
        await _events.PublishAsync(evt, IntegrationPartnerCreatedEvent.EventName, partner.Id.ToString(), cancellationToken);

        return partner.Id;
    }
}
