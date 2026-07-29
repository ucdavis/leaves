using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminDirectoryService
{
    private readonly AppDbContext _db;

    public AdminDirectoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminDirectoryData> LoadDirectoryDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var clusters = await _db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.IsActive)
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);

        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings)
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);

        var people = await _db.People
            .AsNoTracking()
            .OrderBy(person => person.FullName)
            .ThenBy(person => person.IamId)
            .ToListAsync(cancellationToken);
        var appUsers = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.IamId)
            .ToListAsync(cancellationToken);

        var adminIamIdSet = (await _db.AppAdminAssignments
                .AsNoTracking()
                .Select(assignment => assignment.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentOverridesByIamId = await GetCurrentDepartmentOverridesByIamIdAsync(today, cancellationToken);
        var currentChairAssignmentsByDepartment = await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken);
        var currentCaoAssignmentsByCluster = await GetCurrentCaoAssignmentsByClusterAsync(today, cancellationToken);
        var latestAccrualByEmployeeId = await GetLatestAccrualByEmployeeIdAsync(cancellationToken);

        return new AdminDirectoryData(
            AppUsers: appUsers,
            Clusters: clusters,
            Departments: departments,
            People: people,
            AdminIamIdSet: adminIamIdSet,
            CurrentOverridesByIamId: currentOverridesByIamId,
            CurrentChairAssignmentsByDepartment: currentChairAssignmentsByDepartment,
            CurrentCaoAssignmentsByCluster: currentCaoAssignmentsByCluster,
            LatestAccrualByEmployeeId: latestAccrualByEmployeeId);
    }

    public async Task<AdminStatusData> LoadStatusDataAsync(CancellationToken cancellationToken)
    {
        var leaveRequests = await _db.LeaveRequests
            .AsNoTracking()
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .ToListAsync(cancellationToken);

        return new AdminStatusData(LeaveRequests: leaveRequests);
    }

    private async Task<Dictionary<string, EmployeeReportingDepartmentOverride>> GetCurrentDepartmentOverridesByIamIdAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var overrides = await _db.EmployeeReportingDepartmentOverrides
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return overrides
            .GroupBy(item => NormalizeKey(item.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, DepartmentChairAssignment>> GetCurrentChairAssignmentsByDepartmentAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.DepartmentChairAssignments
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.DepartmentCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<int, ClusterCaoAssignment>> GetCurrentCaoAssignmentsByClusterAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.ClusterCaoAssignments
            .AsNoTracking()
            .Where(item => item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.ClusterId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<Dictionary<string, EmployeeAccrualBalance>> GetLatestAccrualByEmployeeIdAsync(CancellationToken cancellationToken)
    {
        var accrualRows = await _db.EmployeeAccrualBalances
            .OrderByDescending(row => row.AsOfDate)
            .ThenByDescending(row => row.LastUpdated)
            .ThenBy(row => row.LeaveTypeNumber)
            .ToListAsync(cancellationToken);

        return accrualRows
            .Where(row => !string.IsNullOrWhiteSpace(row.EmployeeId))
            .GroupBy(row => NormalizeEmployeeId(row.EmployeeId)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string? NormalizeEmployeeId(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public sealed record AdminDirectoryData(
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<Department> Departments,
    IReadOnlyList<Person> People,
    IReadOnlySet<string> AdminIamIdSet,
    IReadOnlyDictionary<string, EmployeeReportingDepartmentOverride> CurrentOverridesByIamId,
    IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
    IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
    IReadOnlyDictionary<string, EmployeeAccrualBalance> LatestAccrualByEmployeeId);

public sealed record AdminStatusData(
    IReadOnlyList<LeaveRequest> LeaveRequests);
