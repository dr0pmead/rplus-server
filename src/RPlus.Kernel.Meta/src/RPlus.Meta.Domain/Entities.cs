namespace RPlus.Meta.Domain.Entities;

public sealed class MetaEntityType
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class MetaFieldDefinition
{
    public Guid Id { get; set; }
    public Guid EntityTypeId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DataType { get; set; } = "text";
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public string? OptionsJson { get; set; }
    public string? ValidationJson { get; set; }
    public string? ReferenceSourceJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MetaFieldType
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? UiSchemaJson { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class MetaEntityRecord
{
    public Guid Id { get; set; }
    public Guid EntityTypeId { get; set; }
    public string? SubjectType { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class MetaFieldValue
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public Guid FieldId { get; set; }
    public string ValueJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public sealed class MetaRelation
{
    public Guid Id { get; set; }
    public Guid FromRecordId { get; set; }
    public Guid ToRecordId { get; set; }
    public string RelationType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class MetaList
{
    public Guid Id { get; set; }
    public Guid? EntityTypeId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SyncMode { get; set; } = "manual";
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class MetaListItem
{
    public Guid Id { get; set; }
    public Guid ListId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ValueJson { get; set; }
    public string? ExternalId { get; set; }
    public Guid? OrganizationNodeId { get; set; }
    public bool IsActive { get; set; } = true;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
}

