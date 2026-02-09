using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Events;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.Kernel.Integration.Application.Features.Partners.Commands;
using RPlus.Kernel.Integration.Application.Features.Partners.Queries;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.SDK.Eventing.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/partners")]
public class IntegrationPartnersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IIntegrationDbContext _db;
    private readonly ILogger<IntegrationPartnersController> _logger;
    private readonly IIntegrationPartnerCache _partnerCache;
    private readonly IEventPublisher _events;

    public IntegrationPartnersController(
        ISender sender,
        IIntegrationDbContext db,
        ILogger<IntegrationPartnersController> logger,
        IIntegrationPartnerCache partnerCache,
        IEventPublisher events)
    {
        _sender = sender;
        _db = db;
        _logger = logger;
        _partnerCache = partnerCache;
        _events = events;
    }

    [HttpGet]
    public async Task<IActionResult> GetPartners()
    {
        var partners = await _db.Partners
            .AsNoTracking()
            .Where(p => p.DeletedAt == null)
            .Select(p => new
            {
                p.Id,
                p.Name,
                Description = p.Description ?? "",
                p.IsActive,
                p.IsDiscountPartner,
                p.DiscountPartner,
                p.AccessLevel,
                ProfileFields = p.ProfileFields,
                p.Metadata,
                p.CreatedAt,
                ApiKeyCount = _db.ApiKeys.Count(k => k.PartnerId == p.Id)
            })
            .ToListAsync();

        return Ok(new
        {
            items = partners,
            totalCount = partners.Count
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPartner(Guid id)
    {
        var partner = await _sender.Send(new GetPartnerQuery(id));
        if (partner == null)
            return NotFound();

        if (partner.IsDiscountPartner)
            partner.ProfileFields = IntegrationPartner.DefaultDiscountProfileFieldKeys.ToList();

        if (partner.ProfileFields is { Count: > 0 })
            partner.ProfileFields = NormalizeProfileFields(partner.ProfileFields).ToList();

        return Ok(partner);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePartner([FromBody] CreatePartnerRequest request)
    {
        if (request.IsDiscountPartner && request.DiscountPartner is null)
        {
            return BadRequest(new { error = "discount_partner_required" });
        }

        var command = new CreatePartnerCommand(
            request.Name,
            request.Description ?? string.Empty,
            request.IsDiscountPartner,
            request.IsDiscountPartner ? request.DiscountPartner : null,
            request.AccessLevel);
        var id = await _sender.Send(command);
        await _partnerCache.InvalidateAsync(id, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetPartner), new { id }, null);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdatePartner(Guid id, [FromBody] UpdatePartnerRequest request)
    {
        var partner = await _db.Partners.FindAsync(id);
        if (partner == null) return NotFound();

        var isDiscountPartner = request.IsDiscountPartner ?? partner.IsDiscountPartner;
        var discountPartner = request.DiscountPartner ?? partner.DiscountPartner;
        if (!isDiscountPartner)
        {
            discountPartner = null;
        }
        else if (discountPartner is null)
        {
            return BadRequest(new { error = "discount_partner_required" });
        }

        partner.Update(
            request.Name ?? partner.Name,
            request.Description ?? partner.Description,
            isDiscountPartner,
            discountPartner,
            request.AccessLevel);

        if (isDiscountPartner)
        {
            partner.UpdateProfileFields(IntegrationPartner.DefaultDiscountProfileFieldKeys);
        }
        else if (request.ProfileFields is not null)
        {
            var normalized = NormalizeProfileFields(request.ProfileFields).ToList();
            partner.UpdateProfileFields(normalized);
        }

        if (request.Metadata is not null)
            partner.UpdateMetadata(request.Metadata);

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
                partner.Activate();
            else
                partner.Deactivate();
        }

        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        await _partnerCache.InvalidateAsync(partner.Id, HttpContext.RequestAborted);

        var updatedEvent = new IntegrationPartnerUpdatedEvent(
            partner.Id,
            partner.Name,
            partner.AccessLevel,
            partner.IsDiscountPartner,
            partner.IsActive,
            DateTime.UtcNow);
        await _events.PublishAsync(updatedEvent, IntegrationPartnerUpdatedEvent.EventName, partner.Id.ToString(), HttpContext.RequestAborted);

        return Ok(partner);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePartner(Guid id)
    {
        var partner = await _db.Partners.FindAsync(id);
        if (partner == null) return NotFound();

        partner.Delete();
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        await _partnerCache.InvalidateAsync(partner.Id, HttpContext.RequestAborted);

        var deletedEvent = new IntegrationPartnerDeletedEvent(partner.Id, partner.DeletedAt ?? DateTime.UtcNow);
        await _events.PublishAsync(deletedEvent, IntegrationPartnerDeletedEvent.EventName, partner.Id.ToString(), HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id}/request-key")]
    public async Task<IActionResult> RequestKey(Guid id)
    {
        var partner = await _db.Partners
            .AsNoTracking()
            .Where(p => p.Id == id && p.DeletedAt == null)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync();

        if (partner == null) return NotFound();

        _logger.LogInformation("Integration key requested for partner {PartnerId} ({PartnerName})", partner.Id, partner.Name);
        var keyRequestedEvent = new IntegrationKeyRequestedEvent(partner.Id, DateTime.UtcNow);
        await _events.PublishAsync(keyRequestedEvent, IntegrationKeyRequestedEvent.EventName, partner.Id.ToString(), HttpContext.RequestAborted);
        return Ok(new { ok = true });
    }

    private static IEnumerable<string> NormalizeProfileFields(IEnumerable<string> fields)
    {
        return fields
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeProfileFieldKey(value.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeProfileFieldKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Trim();
    }

    
}
