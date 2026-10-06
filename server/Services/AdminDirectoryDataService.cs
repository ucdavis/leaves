using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public interface IAdminDirectoryDataService
{
    Task<AdminDirectoryData> LoadDirectoryDataAsync(CancellationToken cancellationToken);
}

public sealed class AdminDirectoryDataService : IAdminDirectoryDataService
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
        var currentOverridesById = await LoadCurrentOverridesByIdAsync(coreData.CurrentFaculty, cancellationToken);
        var currentChairAssignmentsByDepartment = await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken);
        var currentCaoAssignmentsByCluster = await GetCurrentCaoAssignmentsByClusterAsync(today, cancellationToken);

        return new AdminDirectoryData(
            AppUsers: coreData.AppUsers,
            Clusters: coreData.Clusters,
            CurrentCaoAssignmentsByCluster: currentCaoAssignmentsByCluster,
            CurrentChairAssignmentsByDepartment: currentChairAssignmentsByDepartment,
            CurrentFaculty: coreData.CurrentFaculty,
            CurrentOverridesById: currentOverridesById,
            Departments: coreData.Departments,
            AdminIamIds: coreData.AdminIamIds);
    }

    public async Task<IReadOnlyList<CaoDirectoryEmployee>> LoadCaoEmployeesAsync(
        IEnumerable<string> iamIds,
        CancellationToken cancellationToken)
    {
        var ids = iamIds.Select(id => id.Trim()).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await _db.People
            .Where(person => person.IsEmployee == true && ids.Contains(person.IamId))
            .OrderBy(person => person.FullName)
            .ThenBy(person => person.IamId)
            .Select(person => new CaoDirectoryEmployee(person.IamId, person.FullName, person.Email, person.IsFaculty))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FacultyWithOverride>> LoadFacultyWithOverridesAsync(
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utcNow, "Pacific Standard Time"));
        var currentOverrides = _db.EmployeeReportingDepartmentOverrides
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(overrideRecord => overrideRecord.EffectiveStartDate <= today &&
                                     (!overrideRecord.EffectiveEndDateExclusive.HasValue ||
                                      today < overrideRecord.EffectiveEndDateExclusive.Value));
        return await (
                from overrideRecord in currentOverrides
                join person in _db.People.AsNoTracking()
                    on overrideRecord.IamId equals person.IamId into people
                from person in people.DefaultIfEmpty()
                join appUser in _db.AppUsers.AsNoTracking()
                    on overrideRecord.IamId equals appUser.IamId into appUsers
                from appUser in appUsers.DefaultIfEmpty()
                where !currentOverrides.Any(candidate =>
                          candidate.IamId == overrideRecord.IamId &&
                          (candidate.EffectiveStartDate > overrideRecord.EffectiveStartDate ||
                           (candidate.EffectiveStartDate == overrideRecord.EffectiveStartDate &&
                            candidate.Id > overrideRecord.Id)))
                orderby person.FullName, overrideRecord.IamId
                select new FacultyWithOverride(
                    overrideRecord.IamId,
                    person.EmployeeId,
                    person.FullName,
                    person.Email,
                    person != null && person.IsActiveInIam,
                    appUser == null ? null : appUser.IsActive,
                    appUser == null ? null : appUser.DisplayName,
                    overrideRecord.DepartmentCode,
                    overrideRecord.EffectiveStartDate,
                    overrideRecord.EffectiveEndDateExclusive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CurrentRoleAssignmentIds> LoadCurrentRoleAssignmentIdsAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentChairAssignments = await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken);
        var currentCaoAssignments = await GetCurrentCaoAssignmentsByClusterAsync(today, cancellationToken);
        var adminIamIds = (await _db.AppAdminAssignments
                .AsNoTracking()
                .Select(assignment => assignment.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CurrentRoleAssignmentIds(
            AdminIamIds: adminIamIds,
            ChairIamIds: currentChairAssignments.Values
                .Select(assignment => assignment.IamId.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            CaoIamIds: currentCaoAssignments.Values
                .Select(assignment => assignment.IamId.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<string>> SearchEmployeeIdsAsync(
        string? query,
        bool forCao,
        CancellationToken cancellationToken)
    {
        var term = query?.Trim() ?? string.Empty;
        if (term.Length < 2 || term.Length > 128)
        {
            return [];
        }

        var people = _db.People.Where(person => person.IsEmployee == true &&
            ((person.EmployeeId != null && person.EmployeeId.StartsWith(term)) ||
             (person.FullName != null && person.FullName.Contains(term)) ||
             (person.Email != null && person.Email.Contains(term))) &&
            !_db.AppAdminAssignments.Any(assignment => assignment.IamId == person.IamId));

        if (forCao)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            people = people.Where(person =>
                !_db.AppUsers.Any(user => user.IamId == person.IamId && !user.IsActive) &&
                !_db.ClusterCaoAssignments.Any(assignment => assignment.IamId == person.IamId &&
                    assignment.ClosedUtc == null && assignment.EffectiveStartDate <= today &&
                    (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today)) &&
                !_db.DepartmentChairAssignments.Any(assignment => assignment.IamId == person.IamId &&
                    assignment.ClosedUtc == null && assignment.EffectiveStartDate <= today &&
                    (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > today)));
        }

        // Filter and cap in SQL before materializing any directory entries.
        return await people
            .OrderBy(person => person.EmployeeId == term ? 0 : 1)
            .ThenBy(person => person.FullName)
            .ThenBy(person => person.IamId)
                .Select(person => person.IamId)
            .Take(20)
            .ToListAsync(cancellationToken);
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

    public async Task<AdminRoleOptionsData> LoadRoleOptionsDataAsync(
        IEnumerable<string> iamIds,
        CancellationToken cancellationToken)
    {
        var ids = iamIds.Select(id => id.Trim()).Distinct().ToArray();
        return new AdminRoleOptionsData(
            Clusters: await _db.Clusters
                .AsNoTracking()
                .OrderBy(cluster => cluster.ClusterName)
                .ToListAsync(cancellationToken),
            Employees: await _db.People
                .Where(person => person.IsEmployee == true && ids.Contains(person.IamId))
                .OrderBy(person => person.FullName)
                .ThenBy(person => person.IamId)
                .Select(person => new DirectoryEmployee(
                    person.IamId,
                    person.FullName,
                    person.Email,
                    person.EmployeeId))
                .ToListAsync(cancellationToken),
            CurrentFaculty: await _db.CurrentFacultyWithAccrual
                .Where(employee => ids.Contains(employee.IamId))
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

        return await _db.People
            .AnyAsync(person => person.IsEmployee == true && person.IamId == normalizedIamId, cancellationToken);
    }

    public async Task<bool> IsCurrentFacultyInDepartmentAsync(
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

        return await _db.CurrentFacultyWithAccrual
            .AnyAsync(employee => employee.IamId == normalizedIamId &&
                                  employee.ResolvedReportingDepartmentCode == normalizedDepartmentCode,
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
        var currentFaculty = await _db.CurrentFacultyWithAccrual
            .OrderBy(employee => employee.DisplayName)
            .ThenBy(employee => employee.IamId)
            .ToListAsync(cancellationToken);
        var appUsers = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.IamId)
            .ToListAsync(cancellationToken);
        var adminIamIds = (await _db.AppAdminAssignments
                .AsNoTracking()
                .Select(assignment => assignment.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AdminDirectoryCoreData(
            AppUsers: appUsers,
            Clusters: clusters,
            CurrentFaculty: currentFaculty,
            Departments: departments,
            AdminIamIds: adminIamIds);
    }

    private async Task<Dictionary<int, EmployeeReportingDepartmentOverride>> LoadCurrentOverridesByIdAsync(
        IReadOnlyList<CurrentFacultyWithAccrual> currentEmployees,
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
    IReadOnlyList<CurrentFacultyWithAccrual> CurrentFaculty,
    IReadOnlyDictionary<int, EmployeeReportingDepartmentOverride> CurrentOverridesById,
    IReadOnlyList<Department> Departments,
    IReadOnlySet<string> AdminIamIds);

public sealed record AdminStatusDirectoryData(
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
    IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
    IReadOnlyList<Department> Departments);

public sealed record AdminRoleOptionsData(
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<DirectoryEmployee> Employees,
    IReadOnlyList<CurrentFacultyWithAccrual> CurrentFaculty,
    IReadOnlyList<Department> Departments);

public sealed record CaoDirectoryEmployee(string IamId, string? DisplayName, string? Email, bool? IsFaculty);

public sealed record FacultyWithOverride(
    string IamId,
    string? EmployeeId,
    string? FullName,
    string? Email,
    bool IsActiveInIam,
    bool? AppUserIsActive,
    string? AppUserDisplayName,
    string DepartmentCode,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDateExclusive);

public sealed record CurrentRoleAssignmentIds(
    IReadOnlySet<string> AdminIamIds,
    IReadOnlySet<string> ChairIamIds,
    IReadOnlySet<string> CaoIamIds);

public sealed record DirectoryEmployee(
    string IamId,
    string? DisplayName,
    string? Email,
    string? EmployeeId = null);

public sealed record AdminRoleAssignmentsData(
    IReadOnlyList<AppAdminAssignment> AdminAssignments,
    IReadOnlyList<ClusterCaoAssignment> CaoAssignments,
    IReadOnlyList<DepartmentChairAssignment> ChairAssignments);

internal sealed record AdminDirectoryCoreData(
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<CurrentFacultyWithAccrual> CurrentFaculty,
    IReadOnlyList<Department> Departments,
    IReadOnlySet<string> AdminIamIds);
