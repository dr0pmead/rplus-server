using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Application.Validation;
using RPlus.HR.Domain.Entities;
using RPlusGrpc.Hr;

namespace RPlus.HR.Api.Services;

[Authorize]
public sealed class HrGrpcService : HrService.HrServiceBase
{
    private readonly IHrDbContext _db;

    public HrGrpcService(IHrDbContext db)
    {
        _db = db;
    }

    public override async Task<EmployeeVitalStatsResponse> GetEmployeeVitalStats(GetEmployeeVitalStatsRequest request, ServerCallContext context)
    {
        var v2 = await GetVitalStats(new GetVitalStatsRequest { UserId = request.UserId }, context);
        return new EmployeeVitalStatsResponse { TenureDays = v2.TenureDays, IsBirthdayToday = v2.IsBirthdayToday };
    }

    public override async Task<VitalStatsResponse> GetVitalStats(GetVitalStatsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            return new VitalStatsResponse { TenureDays = 0, IsBirthdayToday = false, HasDisability = false, ChildrenCount = 0 };
        }

        var data = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.HireDate,
                x.BirthDate,
                x.DisabilityGroup,
                ChildrenCount = x.FamilyMembers.Count(m => m.Relation == FamilyRelation.Child)
            })
            .FirstOrDefaultAsync(context.CancellationToken);

        if (data == null)
        {
            return new VitalStatsResponse { TenureDays = 0, IsBirthdayToday = false, HasDisability = false, ChildrenCount = 0 };
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenureDays = ComputeTenureDays(data.HireDate, today);
        var isBirthdayToday = IsBirthday(data.BirthDate, today);

        return new VitalStatsResponse
        {
            TenureDays = tenureDays,
            IsBirthdayToday = isBirthdayToday,
            HasDisability = data.DisabilityGroup != DisabilityGroup.None,
            ChildrenCount = data.ChildrenCount
        };
    }

    public override async Task<ProfileByIinResponse> GetProfileByIin(GetProfileByIinRequest request, ServerCallContext context)
    {
        var iin = (request.Iin ?? string.Empty).Trim();
        if (!KzIinValidator.IsValid(iin))
        {
            return new ProfileByIinResponse { Found = false };
        }

        var profile = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => x.Iin == iin)
            .Select(x => new { x.UserId, x.Iin, x.FirstName, x.LastName, x.MiddleName, x.Status })
            .FirstOrDefaultAsync(context.CancellationToken);

        if (profile == null)
            return new ProfileByIinResponse { Found = false };

        return new ProfileByIinResponse
        {
            Found = true,
            UserId = profile.UserId.ToString(),
            Iin = profile.Iin ?? string.Empty,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            MiddleName = profile.MiddleName ?? string.Empty,
            Status = profile.Status.ToString()
        };
    }

    public override async Task<CreateEmployeeResponse> CreateEmployee(CreateEmployeeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UserId"));
        }

        var profile = await _db.EmployeeProfiles.FirstOrDefaultAsync(x => x.UserId == userId, context.CancellationToken);
        if (profile == null)
        {
            profile = new EmployeeProfile
            {
                UserId = userId,
                FirstName = request.FirstName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                MiddleName = request.MiddleName,
                Iin = request.Iin,
                Status = EmployeeStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (request.BirthDate != null)
                profile.BirthDate = DateOnly.FromDateTime(request.BirthDate.ToDateTime());

            if (request.HireDate != null)
                profile.HireDate = DateOnly.FromDateTime(request.HireDate.ToDateTime());
            
            if (request.CustomFields != null && request.CustomFields.Count > 0)
            {
                profile.CustomDataJson = System.Text.Json.JsonSerializer.Serialize(request.CustomFields);
            }

            await _db.EmployeeProfiles.AddAsync(profile, context.CancellationToken);
        }
        else 
        {
             // Idempotency: Update if exists? For now, we update basic info.
             profile.FirstName = request.FirstName ?? profile.FirstName;
             profile.LastName = request.LastName ?? profile.LastName;
             profile.MiddleName = request.MiddleName ?? profile.MiddleName;
             profile.Iin = !string.IsNullOrEmpty(request.Iin) ? request.Iin : profile.Iin;
             
             if (request.CustomFields != null && request.CustomFields.Count > 0)
             {
                 // Merge or Replace? Replaces for now.
                 profile.CustomDataJson = System.Text.Json.JsonSerializer.Serialize(request.CustomFields);
             }

             profile.UpdatedAt = DateTime.UtcNow;
        }

        // TODO: Handle Organization Assignments (OrganizationNodeId, DivisionNodeId... etc)
        // This requires interaction with RPlus.Organization or RPlus.Kernel.Organization
        // Since RPlus.HR doesn't directly own the Org Structure assignments (usually RPlus.Organization does),
        // we might need to publish an event or call another service.
        // For this refactor, we will focus on the Profile creation.

        await _db.SaveChangesAsync(context.CancellationToken);

        return new CreateEmployeeResponse
        {
            EmployeeId = profile.UserId.ToString()
        };
    }

    private static int ComputeTenureDays(DateOnly? hireDate, DateOnly today)
    {
        if (!hireDate.HasValue)
            return 0;

        var days = today.DayNumber - hireDate.Value.DayNumber;
        return days < 0 ? 0 : days;
    }

    private static bool IsBirthday(DateOnly? birthDate, DateOnly today)
    {
        if (!birthDate.HasValue)
            return false;

        var b = birthDate.Value;
        if (b.Month == today.Month && b.Day == today.Day)
            return true;

        // Feb 29th birthdays in non-leap years are commonly celebrated on Feb 28th.
        if (b.Month == 2 && b.Day == 29 && !DateTime.IsLeapYear(today.Year) && today.Month == 2 && today.Day == 28)
            return true;

        return false;
    }

    /// <summary>
    /// Batch retrieve employee profiles by user IDs (for Organization service)
    /// Internal service-to-service call - no user auth required
    /// </summary>
    [AllowAnonymous]
    public override async Task<GetProfilesByUserIdsResponse> GetProfilesByUserIds(
        GetProfilesByUserIdsRequest request,
        ServerCallContext context)
    {
        var response = new GetProfilesByUserIdsResponse();

        var userIds = request.UserIds
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return response;

        var profiles = await _db.EmployeeProfiles
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId,
                x.FirstName,
                x.LastName,
                x.MiddleName,
                x.PhotoFileId,
                Status = x.Status.ToString()
            })
            .ToListAsync(context.CancellationToken);

        foreach (var p in profiles)
        {
            response.Profiles[p.UserId.ToString()] = new HrUserProfile
            {
                UserId = p.UserId.ToString(),
                FirstName = p.FirstName ?? string.Empty,
                LastName = p.LastName ?? string.Empty,
                MiddleName = p.MiddleName ?? string.Empty,
                AvatarId = p.PhotoFileId?.ToString() ?? string.Empty,
                Status = p.Status
            };
        }

        return response;
    }
}
