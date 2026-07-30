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

    internal async Task<AdminDirectoryData> LoadDirectoryDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var clusters = await _db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.IsActive)
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);

        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings.Where(routing => routing.IsActive))
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);

        var people = await _db.People
            .AsNoTracking()
            .OrderBy(person => person.FullName)
            .ThenBy(person => person.IamId)
            .Select(person => new Person
            {
                IamId = person.IamId,
                EmployeeId = person.EmployeeId,
                FullName = person.FullName,
                Email = person.Email,
                IsFaculty = person.IsFaculty,
            })
            .ToListAsync(cancellationToken);
        var appUsers = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.IamId)
            .Select(user => new AppUser
            {
                IamId = user.IamId,
                EmployeeId = user.EmployeeId,
                DisplayName = user.DisplayName,
                Email = user.Email,
                IsActive = user.IsActive,
            })
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

    internal async Task<AdminStatusData> LoadStatusDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var clusterCount = await _db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.IsActive)
            .CountAsync(cancellationToken);
        var clustersMissingCaos = await _db.Clusters
            .AsNoTracking()
            .CountAsync(cluster => cluster.IsActive &&
                !_db.ClusterCaoAssignments.Any(assignment =>
                    assignment.ClusterId == cluster.Id &&
                    assignment.ClosedUtc == null &&
                    assignment.EffectiveStartDate <= today &&
                    (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today)),
                cancellationToken);

        var departmentCount = await _db.Departments
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var departmentsMissingChairs = await _db.Departments
            .AsNoTracking()
            .CountAsync(department => !_db.DepartmentChairAssignments.Any(assignment =>
                    assignment.DepartmentCode == department.DepartmentCode &&
                    assignment.ClosedUtc == null &&
                    assignment.EffectiveStartDate <= today &&
                    (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today)),
                cancellationToken);

        var latestPeoplePromotionAt = await _db.People
            .AsNoTracking()
            .MaxAsync(person => person.PromotedAt, cancellationToken);

        var pendingRequests = await _db.LeaveRequests
            .AsNoTracking()
            .CountAsync(request => request.Status == LeaveRequestStatus.PendingApproval, cancellationToken);

        var accrualCounts = await LatestAccrualRowsQuery()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                LatestUpdatedAt = group.Max(row => (DateTime?)row.LastUpdated),
                ApproachingVacationCap = group.Count(row =>
                    row.TypeLabel.Contains("Vacation") &&
                    (row.ApproachingMax.Trim() == "Y" ||
                     row.ApproachingMax.Trim() == "Yes" ||
                     row.ApproachingMax.Trim() == "True")),
                FacultyAtVacationCap = group.Count(row =>
                    row.TypeLabel.Contains("Vacation") &&
                    row.HoursOverUnderPolicyMax >= 0),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminStatusData(
            ClusterCount: clusterCount,
            ClustersMissingCaos: clustersMissingCaos,
            DepartmentCount: departmentCount,
            DepartmentsMissingChairs: departmentsMissingChairs,
            LatestPeoplePromotionAt: latestPeoplePromotionAt,
            LatestAccrualCount: accrualCounts?.Count ?? 0,
            LatestAccrualUpdatedAt: accrualCounts?.LatestUpdatedAt,
            ApproachingVacationCap: accrualCounts?.ApproachingVacationCap ?? 0,
            FacultyAtVacationCap: accrualCounts?.FacultyAtVacationCap ?? 0,
            PendingRequests: pendingRequests);
    }

    private async Task<Dictionary<string, EmployeeReportingDepartmentOverride>> GetCurrentDepartmentOverridesByIamIdAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var overrides = await _db.EmployeeReportingDepartmentOverrides
            .AsNoTracking()
            .Where(item => item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today) &&
                           !_db.EmployeeReportingDepartmentOverrides.Any(candidate =>
                               candidate.IamId == item.IamId &&
                               candidate.ClosedUtc == null &&
                               candidate.EffectiveStartDate <= today &&
                               (!candidate.EffectiveEndDateExclusive.HasValue || candidate.EffectiveEndDateExclusive.Value > today) &&
                               (candidate.EffectiveStartDate > item.EffectiveStartDate ||
                                (candidate.EffectiveStartDate == item.EffectiveStartDate && candidate.Id > item.Id))))
            .ToListAsync(cancellationToken);

        return overrides
            .ToDictionary(item => NormalizeKey(item.IamId), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, DepartmentChairAssignment>> GetCurrentChairAssignmentsByDepartmentAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.DepartmentChairAssignments
            .AsNoTracking()
            .Where(item => item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today) &&
                           !_db.DepartmentChairAssignments.Any(candidate =>
                               candidate.DepartmentCode == item.DepartmentCode &&
                               candidate.ClosedUtc == null &&
                               candidate.EffectiveStartDate <= today &&
                               (!candidate.EffectiveEndDateExclusive.HasValue || candidate.EffectiveEndDateExclusive.Value > today) &&
                               (candidate.EffectiveStartDate > item.EffectiveStartDate ||
                                (candidate.EffectiveStartDate == item.EffectiveStartDate && candidate.Id > item.Id))))
            .ToListAsync(cancellationToken);

        return assignments
            .ToDictionary(item => item.DepartmentCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<int, ClusterCaoAssignment>> GetCurrentCaoAssignmentsByClusterAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.ClusterCaoAssignments
            .AsNoTracking()
            .Where(item => item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today) &&
                           !_db.ClusterCaoAssignments.Any(candidate =>
                               candidate.ClusterId == item.ClusterId &&
                               candidate.ClosedUtc == null &&
                               candidate.EffectiveStartDate <= today &&
                               (!candidate.EffectiveEndDateExclusive.HasValue || candidate.EffectiveEndDateExclusive.Value > today) &&
                               (candidate.EffectiveStartDate > item.EffectiveStartDate ||
                                (candidate.EffectiveStartDate == item.EffectiveStartDate && candidate.Id > item.Id))))
            .ToListAsync(cancellationToken);

        return assignments
            .ToDictionary(item => item.ClusterId);
    }

    internal Task<List<AdminAccrualRow>> LoadCurrentAccrualRowsAsync(CancellationToken cancellationToken)
    {
        return ProjectAccrualRows(CurrentAccrualRowsQuery())
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<string, AdminAccrualRow>> GetLatestAccrualByEmployeeIdAsync(CancellationToken cancellationToken)
    {
        var accrualRows = await ProjectAccrualRows(LatestAccrualRowsQuery())
            .ToListAsync(cancellationToken);

        return accrualRows
            .ToDictionary(row => NormalizeEmployeeId(row.EmployeeId)!, StringComparer.OrdinalIgnoreCase);
    }

    private IQueryable<EmployeeAccrualBalance> LatestAccrualRowsQuery()
    {
        return _db.EmployeeAccrualBalances
            .AsNoTracking()
            .Where(row => !_db.EmployeeAccrualBalances.Any(candidate =>
                candidate.EmployeeId == row.EmployeeId &&
                (candidate.AsOfDate > row.AsOfDate ||
                 (candidate.AsOfDate == row.AsOfDate &&
                  (candidate.LastUpdated > row.LastUpdated ||
                   (candidate.LastUpdated == row.LastUpdated &&
                    (candidate.LeaveTypeNumber < row.LeaveTypeNumber ||
                     (candidate.LeaveTypeNumber == row.LeaveTypeNumber &&
                      candidate.PositionNumber.CompareTo(row.PositionNumber) < 0))))))));
    }

    private IQueryable<EmployeeAccrualBalance> CurrentAccrualRowsQuery()
    {
        return _db.EmployeeAccrualBalances
            .AsNoTracking()
            .Where(row => !_db.EmployeeAccrualBalances.Any(candidate =>
                candidate.EmployeeId == row.EmployeeId &&
                candidate.AsOfDate > row.AsOfDate));
    }

    private static IQueryable<AdminAccrualRow> ProjectAccrualRows(IQueryable<EmployeeAccrualBalance> query)
    {
        return query.Select(row => new AdminAccrualRow(
            row.EmployeeId,
            row.AsOfDate,
            row.PositionNumber,
            row.LeaveTypeNumber,
            row.EmployeeEmail,
            row.EmployeeName,
            row.EmployeeClassDescription,
            row.JobCodeDescription,
            row.TypeLabel,
            row.ApproachingMax,
            row.HoursOverUnderPolicyMax,
            row.Level5Dept,
            row.LastUpdated));
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

internal sealed record AdminDirectoryData(
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<Department> Departments,
    IReadOnlyList<Person> People,
    IReadOnlySet<string> AdminIamIdSet,
    IReadOnlyDictionary<string, EmployeeReportingDepartmentOverride> CurrentOverridesByIamId,
    IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
    IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
    IReadOnlyDictionary<string, AdminAccrualRow> LatestAccrualByEmployeeId);

internal sealed record AdminStatusData(
    int ClusterCount,
    int ClustersMissingCaos,
    int DepartmentCount,
    int DepartmentsMissingChairs,
    DateTime? LatestPeoplePromotionAt,
    int LatestAccrualCount,
    DateTime? LatestAccrualUpdatedAt,
    int ApproachingVacationCap,
    int FacultyAtVacationCap,
    int PendingRequests);

internal sealed record AdminAccrualRow(
    string EmployeeId,
    DateOnly AsOfDate,
    string PositionNumber,
    int LeaveTypeNumber,
    string? EmployeeEmail,
    string EmployeeName,
    string EmployeeClassDescription,
    string JobCodeDescription,
    string TypeLabel,
    string ApproachingMax,
    decimal HoursOverUnderPolicyMax,
    string Level5Dept,
    DateTime LastUpdated);
