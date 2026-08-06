using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminDirectoryDataService
{
    private readonly AppDbContext _db;

    public AdminDirectoryDataService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminDirectoryData> LoadDirectoryDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var coreData = await LoadDirectoryCoreDataAsync(cancellationToken);
        var currentOverridesById = await LoadCurrentOverridesByIdAsync(coreData.CurrentEmployees, cancellationToken);
        var currentChairAssignmentsByDepartment = await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken);
        var currentCaoAssignmentsByCluster = await GetCurrentCaoAssignmentsByClusterAsync(today, cancellationToken);

        return new AdminDirectoryData(
            AppUsers: coreData.AppUsers,
            Clusters: coreData.Clusters,
            CurrentCaoAssignmentsByCluster: currentCaoAssignmentsByCluster,
            CurrentChairAssignmentsByDepartment: currentChairAssignmentsByDepartment,
            CurrentEmployees: coreData.CurrentEmployees,
            CurrentOverridesById: currentOverridesById,
            Departments: coreData.Departments,
            AdminIamIds: coreData.AdminIamIds,
            NonFacultyIamIds: coreData.NonFacultyIamIds);
    }

    public async Task<AdminStatusDirectoryData> LoadStatusDirectoryDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new AdminStatusDirectoryData(
            Clusters: await _db.Clusters
                .AsNoTracking()
                .OrderBy(cluster => cluster.ClusterName)
                .ToListAsync(cancellationToken),
            CurrentCaoAssignmentsByCluster: await GetCurrentCaoAssignmentsByClusterAsync(today, cancellationToken),
            CurrentChairAssignmentsByDepartment: await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken),
            Departments: await _db.Departments
                .AsNoTracking()
                .OrderBy(department => department.DepartmentName)
                .ToListAsync(cancellationToken));
    }

    public async Task<AdminRoleOptionsData> LoadRoleOptionsDataAsync(CancellationToken cancellationToken)
    {
        return new AdminRoleOptionsData(
            Clusters: await _db.Clusters
                .AsNoTracking()
                .OrderBy(cluster => cluster.ClusterName)
                .ToListAsync(cancellationToken),
            CurrentEmployees: await _db.CurrentEmployees
                .OrderBy(employee => employee.DisplayName)
                .ThenBy(employee => employee.IamId)
                .ToListAsync(cancellationToken),
            Departments: await _db.Departments
                .AsNoTracking()
                .OrderBy(department => department.DepartmentName)
                .ToListAsync(cancellationToken));
    }

    public async Task<AdminRoleAssignmentsData> LoadRoleAssignmentsDataAsync(CancellationToken cancellationToken)
    {
        return new AdminRoleAssignmentsData(
            AdminAssignments: await _db.AppAdminAssignments
                .AsNoTracking()
                .OrderBy(assignment => assignment.IamId)
                .ToListAsync(cancellationToken),
            CaoAssignments: await _db.ClusterCaoAssignments
                .AsNoTracking()
                .OrderBy(assignment => assignment.ClusterId)
                .ThenBy(assignment => assignment.IamId)
                .ToListAsync(cancellationToken),
            ChairAssignments: await _db.DepartmentChairAssignments
                .AsNoTracking()
                .OrderBy(assignment => assignment.DepartmentCode)
                .ThenBy(assignment => assignment.IamId)
                .ToListAsync(cancellationToken));
    }

    public async Task<bool> DirectoryUserExistsAsync(string iamId, CancellationToken cancellationToken)
    {
        var normalizedIamId = iamId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIamId))
        {
            return false;
        }

        return await _db.CurrentEmployees
            .AnyAsync(employee => employee.IamId.Trim() == normalizedIamId, cancellationToken);
    }

    public async Task<bool> UserBelongsToDepartmentAsync(
        string iamId,
        string departmentCode,
        CancellationToken cancellationToken)
    {
        var normalizedIamId = iamId.Trim();
        var normalizedDepartmentCode = departmentCode.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIamId) || string.IsNullOrWhiteSpace(normalizedDepartmentCode))
        {
            return false;
        }

        return await _db.CurrentEmployees
            .AnyAsync(employee =>
                employee.IamId.Trim() == normalizedIamId &&
                employee.ResolvedReportingDepartmentCode != null &&
                employee.ResolvedReportingDepartmentCode.Trim() == normalizedDepartmentCode,
                cancellationToken);
    }

    private async Task<AdminDirectoryCoreData> LoadDirectoryCoreDataAsync(CancellationToken cancellationToken)
    {
        var clusters = await _db.Clusters
            .AsNoTracking()
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);
        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings)
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);
        var currentEmployees = await _db.CurrentEmployees
            .OrderBy(employee => employee.DisplayName)
            .ThenBy(employee => employee.IamId)
            .ToListAsync(cancellationToken);
        var appUsers = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.IamId)
            .ToListAsync(cancellationToken);
        var nonFacultyIamIds = (await _db.People
                .Where(person => person.IsEmployee == true && person.IsFaculty == false)
                .Select(person => person.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var adminIamIds = (await _db.AppAdminAssignments
                .AsNoTracking()
                .Select(assignment => assignment.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AdminDirectoryCoreData(
            AppUsers: appUsers,
            Clusters: clusters,
            CurrentEmployees: currentEmployees,
            Departments: departments,
            AdminIamIds: adminIamIds,
            NonFacultyIamIds: nonFacultyIamIds);
    }

    private async Task<Dictionary<int, EmployeeReportingDepartmentOverride>> LoadCurrentOverridesByIdAsync(
        IReadOnlyList<CurrentEmployee> currentEmployees,
        CancellationToken cancellationToken)
    {
        var currentOverrideIds = currentEmployees
            .Select(employee => employee.ReportingDepartmentOverrideId)
            .OfType<int>()
            .Distinct()
            .ToList();

        return currentOverrideIds.Count == 0
            ? []
            : await _db.EmployeeReportingDepartmentOverrides
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => currentOverrideIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private async Task<Dictionary<string, DepartmentChairAssignment>> GetCurrentChairAssignmentsByDepartmentAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.DepartmentChairAssignments
            .AsNoTracking()
            .Where(item => item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
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
            .Where(item => item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.ClusterId)
            .ToDictionary(group => group.Key, group => group.First());
    }
}

public sealed record AdminDirectoryData(
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
    IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
    IReadOnlyList<CurrentEmployee> CurrentEmployees,
    IReadOnlyDictionary<int, EmployeeReportingDepartmentOverride> CurrentOverridesById,
    IReadOnlyList<Department> Departments,
    IReadOnlySet<string> AdminIamIds,
    IReadOnlySet<string> NonFacultyIamIds);

public sealed record AdminStatusDirectoryData(
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
    IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
    IReadOnlyList<Department> Departments);

public sealed record AdminRoleOptionsData(
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<CurrentEmployee> CurrentEmployees,
    IReadOnlyList<Department> Departments);

public sealed record AdminRoleAssignmentsData(
    IReadOnlyList<AppAdminAssignment> AdminAssignments,
    IReadOnlyList<ClusterCaoAssignment> CaoAssignments,
    IReadOnlyList<DepartmentChairAssignment> ChairAssignments);

internal sealed record AdminDirectoryCoreData(
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<CurrentEmployee> CurrentEmployees,
    IReadOnlyList<Department> Departments,
    IReadOnlySet<string> AdminIamIds,
    IReadOnlySet<string> NonFacultyIamIds);
