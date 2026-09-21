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

    public async Task<AdminDepartmentSummaryData> LoadDepartmentSummaryDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var clusters = await _db.Clusters
            .AsNoTracking()
            .OrderBy(cluster => cluster.ClusterName)
            .ToListAsync(cancellationToken);
        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings)
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);
        var chairAssignments = await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken);
        var caoAssignments = await GetCurrentCaoAssignmentsByClusterAsync(today, cancellationToken);
        var linkedUserCounts = (await _db.CurrentEmployees
                .Where(employee => employee.ResolvedReportingDepartmentCode != null)
                .GroupBy(employee => employee.ResolvedReportingDepartmentCode!)
                .Select(group => new DepartmentUserCount(group.Key, group.Count()))
                .ToListAsync(cancellationToken))
            .ToDictionary(
                item => item.DepartmentCode.Trim(),
                item => item.Count,
                StringComparer.OrdinalIgnoreCase);
        var assignmentIamIds = chairAssignments.Values
            .Select(assignment => assignment.IamId)
            .Concat(caoAssignments.Values.Select(assignment => assignment.IamId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assignmentNamesByIamId = await LoadDisplayNamesByIamIdAsync(assignmentIamIds, cancellationToken);

        return new AdminDepartmentSummaryData(
            AssignmentNamesByIamId: assignmentNamesByIamId,
            Clusters: clusters,
            CurrentCaoAssignmentsByCluster: caoAssignments,
            CurrentChairAssignmentsByDepartment: chairAssignments,
            Departments: departments,
            LinkedUserCountsByDepartment: linkedUserCounts);
    }

    public async Task<AdminDepartmentRosterData> LoadDepartmentRosterDataAsync(
        string departmentCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedDepartmentCode = departmentCode.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentCaoIamIds = _db.ClusterCaoAssignments
            .Where(assignment => assignment.ClosedUtc == null &&
                                 assignment.EffectiveStartDate <= today &&
                                 (!assignment.EffectiveEndDateExclusive.HasValue ||
                                  assignment.EffectiveEndDateExclusive.Value > today))
            .Select(assignment => assignment.IamId);
        var rosterQuery = _db.CurrentEmployees
            .Where(employee => employee.ResolvedReportingDepartmentCode == normalizedDepartmentCode)
            .Where(employee => !currentCaoIamIds.Contains(employee.IamId))
            .Where(employee => !_db.People.Any(person =>
                person.IamId == employee.IamId &&
                person.IsEmployee == true &&
                person.IsFaculty == false));
        var totalCount = await rosterQuery.CountAsync(cancellationToken);
        var employees = await rosterQuery
            .OrderBy(employee => employee.DisplayName)
            .ThenBy(employee => employee.IamId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var iamIds = employees.Select(employee => employee.IamId).ToList();
        var appUsers = iamIds.Count == 0
            ? []
            : await _db.AppUsers
                .AsNoTracking()
                .Where(user => iamIds.Contains(user.IamId))
                .ToListAsync(cancellationToken);
        var overridesById = await LoadCurrentOverridesByIdAsync(employees, cancellationToken);
        var chairAssignments = await _db.DepartmentChairAssignments
            .AsNoTracking()
            .Where(assignment => assignment.DepartmentCode == normalizedDepartmentCode &&
                                 assignment.ClosedUtc == null &&
                                 assignment.EffectiveStartDate <= today &&
                                 (!assignment.EffectiveEndDateExclusive.HasValue ||
                                  assignment.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(assignment => assignment.EffectiveStartDate)
            .ThenByDescending(assignment => assignment.Id)
            .ToListAsync(cancellationToken);
        var adminIamIds = iamIds.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.AppAdminAssignments
                    .AsNoTracking()
                    .Where(assignment => iamIds.Contains(assignment.IamId))
                    .Select(assignment => assignment.IamId.Trim())
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nonFacultyIamIds = iamIds.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.People
                    .Where(person => iamIds.Contains(person.IamId) &&
                                     person.IsEmployee == true &&
                                     person.IsFaculty == false)
                    .Select(person => person.IamId.Trim())
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AdminDepartmentRosterData(
            AdminIamIds: adminIamIds,
            AppUsers: appUsers,
            CurrentChairAssignments: chairAssignments,
            CurrentEmployees: employees,
            CurrentOverridesById: overridesById,
            NonFacultyIamIds: nonFacultyIamIds,
            TotalCount: totalCount);
    }

    public async Task<IReadOnlyList<AdminDirectorySearchUser>> SearchCaoCandidatesAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        var candidates =
            from employee in _db.CurrentEmployees
            join person in _db.People on employee.IamId equals person.IamId
            join appUser in _db.AppUsers.AsNoTracking() on employee.IamId equals appUser.IamId into appUsers
            from appUser in appUsers.DefaultIfEmpty()
            where person.IsEmployee == true && person.IsFaculty == false &&
                  (appUser == null || appUser.IsActive)
            where string.IsNullOrWhiteSpace(normalizedQuery) ||
                  employee.IamId.Contains(normalizedQuery) ||
                  (employee.DisplayName != null && employee.DisplayName.Contains(normalizedQuery)) ||
                  (employee.Email != null && employee.Email.Contains(normalizedQuery))
            orderby employee.DisplayName, employee.IamId
            select new AdminDirectorySearchUser(
                employee.IamId.Trim(),
                employee.DisplayName ?? appUser!.DisplayName ?? employee.IamId,
                employee.Email ?? appUser!.Email ?? string.Empty);

        return await candidates.Take(10).ToListAsync(cancellationToken);
    }

    public async Task<AdminDirectoryData> LoadFacultyDirectoryDataAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var facultyEmployees = FacultyEmployeesQuery(today);

        var departments = await _db.Departments
            .AsNoTracking()
            .Include(department => department.DepartmentEmailRoutings)
            .OrderBy(department => department.DepartmentName)
            .ToListAsync(cancellationToken);
        var currentEmployees = await facultyEmployees
            .OrderBy(employee => employee.DisplayName)
            .ThenBy(employee => employee.IamId)
            .ToListAsync(cancellationToken);
        var appUsers = await (
                from appUser in _db.AppUsers.AsNoTracking()
                join employee in FacultyEmployeesQuery(today) on appUser.IamId equals employee.IamId
                select appUser)
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.IamId)
            .ToListAsync(cancellationToken);
        var currentOverridesById = await LoadCurrentOverridesByIdAsync(currentEmployees, cancellationToken);
        var currentChairAssignmentsByDepartment = await GetCurrentChairAssignmentsByDepartmentAsync(today, cancellationToken);
        var adminIamIds = (await _db.AppAdminAssignments
                .AsNoTracking()
                .Select(assignment => assignment.IamId.Trim())
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AdminDirectoryData(
            AppUsers: appUsers,
            Clusters: [],
            CurrentCaoAssignmentsByCluster: new Dictionary<int, ClusterCaoAssignment>(),
            CurrentChairAssignmentsByDepartment: currentChairAssignmentsByDepartment,
            CurrentEmployees: currentEmployees,
            CurrentOverridesById: currentOverridesById,
            Departments: departments,
            AdminIamIds: adminIamIds,
            NonFacultyIamIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
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

        return await (
                from employee in _db.CurrentEmployees
                join person in _db.People on employee.IamId equals person.IamId
                where employee.IamId.Trim() == normalizedIamId &&
                      employee.ResolvedReportingDepartmentCode != null &&
                      employee.ResolvedReportingDepartmentCode.Trim() == normalizedDepartmentCode &&
                      person.IsEmployee == true &&
                      person.IsFaculty == true
                select employee.IamId)
            .AnyAsync(cancellationToken);
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

    private IQueryable<CurrentEmployee> FacultyEmployeesQuery(DateOnly today)
    {
        var currentCaoIamIds = _db.ClusterCaoAssignments
            .Where(assignment => assignment.ClosedUtc == null &&
                                 assignment.EffectiveStartDate <= today &&
                                 (!assignment.EffectiveEndDateExclusive.HasValue ||
                                  assignment.EffectiveEndDateExclusive.Value > today))
            .Select(assignment => assignment.IamId);

        return _db.CurrentEmployees
            .Where(employee => employee.HasCurrentAccrualRecord)
            .Where(employee => !currentCaoIamIds.Contains(employee.IamId))
            .Where(employee => !_db.People.Any(person =>
                person.IamId == employee.IamId &&
                person.IsEmployee == true &&
                person.IsFaculty == false));
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadDisplayNamesByIamIdAsync(
        IReadOnlyCollection<string> iamIds,
        CancellationToken cancellationToken)
    {
        if (iamIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var employeeNames = await _db.CurrentEmployees
            .Where(employee => iamIds.Contains(employee.IamId))
            .Select(employee => new DirectoryDisplayName(employee.IamId.Trim(), employee.DisplayName))
            .ToListAsync(cancellationToken);
        var appUserNames = await _db.AppUsers
            .AsNoTracking()
            .Where(user => iamIds.Contains(user.IamId))
            .Select(user => new DirectoryDisplayName(user.IamId.Trim(), user.DisplayName))
            .ToListAsync(cancellationToken);

        return employeeNames
            .Concat(appUserNames)
            .Where(item => !string.IsNullOrWhiteSpace(item.DisplayName))
            .GroupBy(item => item.IamId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().DisplayName!, StringComparer.OrdinalIgnoreCase);
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

public sealed record AdminDepartmentSummaryData(
    IReadOnlyDictionary<string, string> AssignmentNamesByIamId,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyDictionary<int, ClusterCaoAssignment> CurrentCaoAssignmentsByCluster,
    IReadOnlyDictionary<string, DepartmentChairAssignment> CurrentChairAssignmentsByDepartment,
    IReadOnlyList<Department> Departments,
    IReadOnlyDictionary<string, int> LinkedUserCountsByDepartment);

public sealed record AdminDepartmentRosterData(
    IReadOnlySet<string> AdminIamIds,
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<DepartmentChairAssignment> CurrentChairAssignments,
    IReadOnlyList<CurrentEmployee> CurrentEmployees,
    IReadOnlyDictionary<int, EmployeeReportingDepartmentOverride> CurrentOverridesById,
    IReadOnlySet<string> NonFacultyIamIds,
    int TotalCount);

public sealed record AdminDirectorySearchUser(string Id, string Name, string Email);

internal sealed record DepartmentUserCount(string DepartmentCode, int Count);
internal sealed record DirectoryDisplayName(string IamId, string? DisplayName);

internal sealed record AdminDirectoryCoreData(
    IReadOnlyList<AppUser> AppUsers,
    IReadOnlyList<Cluster> Clusters,
    IReadOnlyList<CurrentEmployee> CurrentEmployees,
    IReadOnlyList<Department> Departments,
    IReadOnlySet<string> AdminIamIds,
    IReadOnlySet<string> NonFacultyIamIds);
