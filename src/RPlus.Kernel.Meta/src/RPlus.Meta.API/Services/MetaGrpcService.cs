using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Meta.Api.Authentication;
using RPlus.Meta.Infrastructure.Persistence;
using RPlusGrpc.Meta;
using System.Text.Json;
using DomainMetaListItem = RPlus.Meta.Domain.Entities.MetaListItem;
using GrpcMetaListItem = RPlusGrpc.Meta.MetaListItem;

namespace RPlus.Meta.Api.Services;

public sealed class MetaGrpcService : MetaService.MetaServiceBase
{
    private readonly MetaDbContext _db;
    private readonly IOptionsMonitor<ServiceSecretAuthenticationOptions> _authOptions;

    public MetaGrpcService(MetaDbContext db, IOptionsMonitor<ServiceSecretAuthenticationOptions> authOptions)
    {
        _db = db;
        _authOptions = authOptions;
    }

    public override async Task<MetaListResponse> GetListByKey(GetListByKeyRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new MetaListResponse { Found = false };
        }

        var list = await _db.Lists.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, context.CancellationToken);
        if (list == null)
        {
            return new MetaListResponse { Found = false };
        }

        return new MetaListResponse
        {
            Found = true,
            List = new MetaList
            {
                Id = list.Id.ToString(),
                Key = list.Key,
                Title = list.Title,
                Description = list.Description ?? string.Empty,
                SyncMode = list.SyncMode ?? string.Empty,
                IsSystem = list.IsSystem,
                IsActive = list.IsActive,
                CreatedAt = Timestamp.FromDateTime(EnsureUtc(list.CreatedAt))
            }
        };
    }

    public override async Task<MetaListItemsResponse> GetListItems(GetListItemsRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        if (!Guid.TryParse(request.ListId, out var listId))
        {
            return new MetaListItemsResponse();
        }

        var items = await _db.ListItems.AsNoTracking()
            .Where(x => x.ListId == listId && x.IsActive)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Title)
            .ToListAsync(context.CancellationToken);

        var response = new MetaListItemsResponse();
        foreach (var item in items)
        {
            response.Items.Add(new GrpcMetaListItem
            {
                Id = item.Id.ToString(),
                ListId = item.ListId.ToString(),
                Code = item.Code,
                Title = item.Title,
                ValueJson = item.ValueJson ?? string.Empty,
                ExternalId = item.ExternalId ?? string.Empty,
                IsActive = item.IsActive,
                Order = item.Order,
                CreatedAt = Timestamp.FromDateTime(EnsureUtc(item.CreatedAt))
            });
        }

        return response;
    }

    public override async Task<GetListFieldsResponse> GetListFields(GetListFieldsRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        if (!Guid.TryParse(request.ListId, out var listId))
            return new GetListFieldsResponse();

        var list = await _db.Lists.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == listId, context.CancellationToken);
        if (list == null || list.EntityTypeId == null)
            return new GetListFieldsResponse();

        var fields = await _db.FieldDefinitions.AsNoTracking()
            .Where(x => x.EntityTypeId == list.EntityTypeId.Value && x.IsActive)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key)
            .ToListAsync(context.CancellationToken);

        var response = new GetListFieldsResponse();
        foreach (var field in fields)
        {
            response.Fields.Add(new MetaFieldDefinition
            {
                Id = field.Id.ToString(),
                Key = field.Key,
                Title = field.Title,
                DataType = field.DataType,
                OptionsJson = field.OptionsJson ?? string.Empty,
                IsRequired = field.IsRequired,
                IsActive = field.IsActive,
                Order = field.Order
            });
        }

        return response;
    }

    public override async Task<GetEntityFieldsResponse> GetEntityFields(GetEntityFieldsRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        var key = (request.EntityTypeKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            return new GetEntityFieldsResponse();

        var entityType = await _db.EntityTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, context.CancellationToken);
        if (entityType == null)
            return new GetEntityFieldsResponse();

        var fields = await _db.FieldDefinitions.AsNoTracking()
            .Where(x => x.EntityTypeId == entityType.Id && x.IsActive)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key)
            .ToListAsync(context.CancellationToken);

        var response = new GetEntityFieldsResponse();
        foreach (var field in fields)
        {
            response.Fields.Add(new MetaFieldDefinition
            {
                Id = field.Id.ToString(),
                Key = field.Key,
                Title = field.Title,
                DataType = field.DataType,
                OptionsJson = field.OptionsJson ?? string.Empty,
                IsRequired = field.IsRequired,
                IsActive = field.IsActive,
                Order = field.Order
            });
        }

        return response;
    }

    public override async Task<GetEntityFieldValuesResponse> GetEntityFieldValues(GetEntityFieldValuesRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        var key = (request.EntityTypeKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            return new GetEntityFieldValuesResponse();

        if (!Guid.TryParse(request.SubjectId, out var subjectId))
            return new GetEntityFieldValuesResponse();

        var entityType = await _db.EntityTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, context.CancellationToken);
        if (entityType == null)
            return new GetEntityFieldValuesResponse();

        var requestedKeys = request.FieldKeys?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        if (requestedKeys.Count == 0)
            return new GetEntityFieldValuesResponse();

        var fields = await _db.FieldDefinitions.AsNoTracking()
            .Where(x => x.EntityTypeId == entityType.Id && requestedKeys.Contains(x.Key))
            .ToListAsync(context.CancellationToken);

        if (fields.Count == 0)
            return new GetEntityFieldValuesResponse();

        var record = await _db.Records.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EntityTypeId == entityType.Id
                  && x.SubjectId == subjectId
                  && x.SubjectType == key,
                context.CancellationToken);

        if (record == null)
            return new GetEntityFieldValuesResponse();

        var fieldIds = fields.Select(x => x.Id).ToList();
        var values = await _db.FieldValues.AsNoTracking()
            .Where(x => x.RecordId == record.Id && fieldIds.Contains(x.FieldId))
            .ToListAsync(context.CancellationToken);

        var response = new GetEntityFieldValuesResponse();
        foreach (var field in fields)
        {
            var value = values.FirstOrDefault(x => x.FieldId == field.Id);
            if (value == null)
                continue;

            response.Values.Add(new MetaFieldValue
            {
                Key = field.Key,
                ValueJson = value.ValueJson ?? string.Empty
            });
        }

        return response;
    }

    public override async Task<UpsertListItemsResponse> UpsertListItems(UpsertListItemsRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        if (!Guid.TryParse(request.ListId, out var listId))
            return new UpsertListItemsResponse();

        if (request.Items == null || request.Items.Count == 0)
            return new UpsertListItemsResponse();

        var results = new UpsertListItemsResponse();
        var normalized = new List<UpsertCandidate>();
        var hadErrors = false;

        foreach (var item in request.Items)
        {
            var externalId = NormalizeOptional(item.ExternalId, 128);
            var code = NormalizeKey(string.IsNullOrWhiteSpace(item.Code) ? externalId : item.Code);
            if (string.IsNullOrWhiteSpace(code))
            {
                results.Results.Add(new UpsertListItemResult
                {
                    ExternalId = externalId ?? string.Empty,
                    Code = string.Empty,
                    Status = "failed",
                    Error = "missing_external_id"
                });
                hadErrors = true;
                if (request.Strict)
                    return results;
                continue;
            }

            var valueJson = NormalizeJson(item.ValueJson);
            if (item.ValueJson != null && valueJson == null)
            {
                results.Results.Add(new UpsertListItemResult
                {
                    ExternalId = externalId ?? string.Empty,
                    Code = code,
                    Status = "failed",
                    Error = "invalid_value_json"
                });
                hadErrors = true;
                if (request.Strict)
                    return results;
                continue;
            }

            normalized.Add(new UpsertCandidate(
                ExternalId: externalId,
                Code: code,
                Title: NormalizeTitle(item.Title),
                ValueJson: valueJson,
                IsActive: item.IsActive,
                Order: item.Order));
        }

        if (normalized.Count == 0)
            return results;

        var externalIds = normalized
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
            .Select(x => x.ExternalId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var codes = normalized
            .Select(x => x.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _db.ListItems
            .Where(x => x.ListId == listId
                && ((x.ExternalId != null && externalIds.Contains(x.ExternalId))
                    || codes.Contains(x.Code)))
            .ToListAsync(context.CancellationToken);

        foreach (var candidate in normalized)
        {
            DomainMetaListItem? target = null;
            if (!string.IsNullOrWhiteSpace(candidate.ExternalId))
            {
                target = existing.FirstOrDefault(x =>
                    string.Equals(x.ExternalId, candidate.ExternalId, StringComparison.OrdinalIgnoreCase));
            }

            if (target == null)
            {
                target = existing.FirstOrDefault(x =>
                    string.Equals(x.Code, candidate.Code, StringComparison.OrdinalIgnoreCase));
            }

            var isNew = target == null;
            if (isNew)
            {
                target = new DomainMetaListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = listId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ListItems.Add(target);
            }

            target.Code = candidate.Code;
            target.ExternalId = candidate.ExternalId;
            target.Title = candidate.Title;
            target.IsActive = candidate.IsActive;
            target.Order = candidate.Order;

            if (!string.IsNullOrWhiteSpace(candidate.ValueJson))
                target.ValueJson = candidate.ValueJson;

            results.Results.Add(new UpsertListItemResult
            {
                ExternalId = candidate.ExternalId ?? string.Empty,
                Code = candidate.Code,
                Status = isNew ? "created" : "updated",
                Error = string.Empty
            });
        }

        if (!hadErrors || !request.Strict)
        {
            await _db.SaveChangesAsync(context.CancellationToken);
        }

        return results;
    }

    public override async Task<DeleteListItemsResponse> DeleteListItems(DeleteListItemsRequest request, ServerCallContext context)
    {
        ValidateSecret(context);

        if (!Guid.TryParse(request.ListId, out var listId))
            return new DeleteListItemsResponse();

        if (request.ExternalId == null || request.ExternalId.Count == 0)
            return new DeleteListItemsResponse();

        var externalIds = request.ExternalId
            .Select(x => NormalizeOptional(x, 128))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (externalIds.Count == 0)
            return new DeleteListItemsResponse();

        var items = await _db.ListItems
            .Where(x => x.ListId == listId && x.ExternalId != null && externalIds.Contains(x.ExternalId))
            .ToListAsync(context.CancellationToken);

        var response = new DeleteListItemsResponse();
        foreach (var externalId in externalIds)
        {
            var item = items.FirstOrDefault(x => string.Equals(x.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                response.Results.Add(new DeleteListItemResult
                {
                    ExternalId = externalId,
                    Status = "not_found",
                    Error = "not_found"
                });
                continue;
            }

            _db.ListItems.Remove(item);
            response.Results.Add(new DeleteListItemResult
            {
                ExternalId = externalId,
                Status = "deleted",
                Error = string.Empty
            });
        }

        await _db.SaveChangesAsync(context.CancellationToken);
        return response;
    }

    private void ValidateSecret(ServerCallContext context)
    {
        var expected = (_authOptions.CurrentValue.SharedSecret ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        var actual = context.RequestHeaders.GetValue("x-rplus-service-secret") ?? string.Empty;
        if (!string.Equals(actual.Trim(), expected, StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "invalid_service_secret"));
        }
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string NormalizeKey(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeTitle(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        return s.Length == 0 ? "Untitled" : s;
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    private static string? NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var _ = JsonDocument.Parse(json);
            return json.Trim();
        }
        catch
        {
            return null;
        }
    }

    private sealed record UpsertCandidate(
        string? ExternalId,
        string Code,
        string Title,
        string? ValueJson,
        bool IsActive,
        int Order);
}
