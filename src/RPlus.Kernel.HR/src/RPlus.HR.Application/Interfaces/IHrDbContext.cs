using Microsoft.EntityFrameworkCore;
using RPlus.HR.Domain.Entities;

namespace RPlus.HR.Application.Interfaces;

public interface IHrDbContext
{
    DbSet<EmployeeProfile> EmployeeProfiles { get; }
    // DbSet<EmployeeDocument> EmployeeDocuments { get; }
    // DbSet<FamilyMember> FamilyMembers { get; }
    // DbSet<FamilyMemberDocument> FamilyMemberDocuments { get; }
    DbSet<HrFile> HrFiles { get; }
    DbSet<MilitaryRecord> MilitaryRecords { get; }
    DbSet<BankDetails> BankDetails { get; }
    DbSet<HrCustomFieldDefinition> CustomFieldDefinitions { get; }
    DbSet<HrCustomFieldValue> CustomFieldValues { get; }
    DbSet<HrAuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
