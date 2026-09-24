using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminRolesService
{
    private readonly AdminDirectoryDataService _directoryDataService;

    public AdminRolesService(AdminDirectoryDataService directoryDataService)
    {
        _directoryDataService = directoryDataService;
    }

    public async Task<AdminRolesResponse> GetRolesAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var roleAssignmentsData = await _directoryDataService.LoadRoleAssignmentsDataAsync(cancellationToken);
        var iamIds = roleAssignmentsData.AdminAssignments.Select(assignment => assignment.IamId)
            .Concat(roleAssignmentsData.CaoAssignments.Select(assignment => assignment.IamId))
            .Concat(roleAssignmentsData.ChairAssignments.Select(assignment => assignment.IamId));
        var roleOptionsData = await _directoryDataService.LoadRoleOptionsDataAsync(iamIds, cancellationToken);

        return BuildRolesResponse(roleOptionsData, roleAssignmentsData, today);
    }

    public async Task<IReadOnlyList<AdminRoleUserOption>> SearchAdminCandidatesAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var ids = await _directoryDataService.SearchEmployeeIdsAsync(query, forCao: false, cancellationToken);
        if (ids.Count == 0)
        {
            return [];
        }

        var options = await _directoryDataService.LoadRoleOptionsDataAsync(ids, cancellationToken);
        var idOrder = ids.Select((id, index) => (Id: id.Trim(), Index: index))
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.OrdinalIgnoreCase);
        return BuildUserOptions(options)
            .OrderBy(user => idOrder[user.IamId])
            .ToList();
    }

    internal static InactiveRoleAssignmentChanges GetInactiveRoleAssignmentChanges(
        IReadOnlyList<AppAdminAssignment> adminAssignments,
        IReadOnlyList<ClusterCaoAssignment> caoAssignments,
        IReadOnlyList<DepartmentChairAssignment> chairAssignments,
        IReadOnlyList<DirectoryEmployee> employees,
        IReadOnlyList<CurrentFacultyWithAccrual> currentFaculty,
        IReadOnlyList<Cluster> clusters,
        IReadOnlyList<Department> departments)
    {
        var employeeIamIds = employees
            .Where(employee => !string.IsNullOrWhiteSpace(employee.IamId))
            .Select(employee => employee.IamId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentFacultyByIamId = currentFaculty
            .Where(employee => !string.IsNullOrWhiteSpace(employee.IamId))
            .GroupBy(employee => employee.IamId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var activeClusterIds = clusters
            .Where(cluster => cluster.IsActive)
            .Select(cluster => cluster.Id)
            .ToHashSet();
        var activeDepartmentCodes = departments
            .Where(department => department.IsActive)
            .Select(department => department.DepartmentCode.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inactiveAdminAssignments = adminAssignments
            .Where(assignment => !employeeIamIds.Contains(assignment.IamId.Trim()))
            .ToList();

        var inactiveCaoAssignments = caoAssignments
            .Where(assignment =>
                !employeeIamIds.Contains(assignment.IamId.Trim()) ||
                !activeClusterIds.Contains(assignment.ClusterId))
            .ToList();

        var inactiveChairAssignments = chairAssignments
            .Where(assignment =>
                !currentFacultyByIamId.TryGetValue(assignment.IamId.Trim(), out var faculty) ||
                !activeDepartmentCodes.Contains(assignment.DepartmentCode.Trim()) ||
                !string.Equals(
                    faculty.ResolvedReportingDepartmentCode?.Trim(),
                    assignment.DepartmentCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new InactiveRoleAssignmentChanges(
            AdminAssignmentsToDelete: inactiveAdminAssignments,
            CaoAssignmentsToClose: inactiveCaoAssignments,
            ChairAssignmentsToClose: inactiveChairAssignments);
    }

    internal static void CloseClusterCaoAssignment(
        ClusterCaoAssignment assignment,
        int? closedByAppUserId,
        DateTime closedUtc,
        DateOnly today)
    {
        if (closedByAppUserId.HasValue)
        {
            assignment.ClosedByAppUserId = closedByAppUserId.Value;
        }

        assignment.ClosedUtc = closedUtc;
        assignment.EffectiveEndDateExclusive = GetEffectiveEndDateExclusive(
            assignment.EffectiveEndDateExclusive,
            today);
    }

    internal static void CloseDepartmentChairAssignment(
        DepartmentChairAssignment assignment,
        int? closedByAppUserId,
        DateTime closedUtc,
        DateOnly today)
    {
        if (closedByAppUserId.HasValue)
        {
            assignment.ClosedByAppUserId = closedByAppUserId.Value;
        }

        assignment.ClosedUtc = closedUtc;
        assignment.EffectiveEndDateExclusive = GetEffectiveEndDateExclusive(
            assignment.EffectiveEndDateExclusive,
            today);
    }

    internal static AdminRolesResponse BuildRolesResponse(
        AdminRoleOptionsData roleOptionsData,
        AdminRoleAssignmentsData roleAssignmentsData,
        DateOnly today)
    {
        var employeesByIamId = roleOptionsData.Employees
            .Where(employee => !string.IsNullOrWhiteSpace(employee.IamId))
            .GroupBy(employee => employee.IamId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var currentFacultyByIamId = roleOptionsData.CurrentFaculty
            .Where(employee => !string.IsNullOrWhiteSpace(employee.IamId))
            .GroupBy(employee => employee.IamId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var departments = roleOptionsData.Departments;
        var departmentsByCode = departments.ToDictionary(department => department.DepartmentCode, StringComparer.OrdinalIgnoreCase);
        var clusters = roleOptionsData.Clusters;
        var clustersById = clusters.ToDictionary(cluster => cluster.Id);

        var assignments = roleAssignmentsData.AdminAssignments
            .Select(assignment => CreateAssignmentResponse(
                active: IsRoleAssignmentActive(
                    isEligible: employeesByIamId.ContainsKey(assignment.IamId.Trim())),
                effectiveEndDate: null,
                effectiveStartDate: null,
                id: assignment.Id.ToString(),
                iamId: assignment.IamId,
                targetId: null,
                targetName: null,
                type: "admin",
                employeesByIamId: employeesByIamId))
            .Concat(roleAssignmentsData.CaoAssignments.Select(assignment =>
            {
                clustersById.TryGetValue(assignment.ClusterId, out var cluster);
                return CreateAssignmentResponse(
                    active: IsRoleAssignmentActive(
                        isEligible: employeesByIamId.ContainsKey(assignment.IamId.Trim()),
                        targetIsActive: cluster?.IsActive ?? false,
                        startDate: assignment.EffectiveStartDate,
                        endDate: assignment.EffectiveEndDateExclusive,
                        today: today,
                        closedUtc: assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: assignment.ClusterId.ToString(),
                    targetName: cluster?.ClusterName ?? $"Cluster {assignment.ClusterId}",
                    type: "cao",
                    employeesByIamId: employeesByIamId);
            }))
            .Concat(roleAssignmentsData.ChairAssignments.Select(assignment =>
            {
                var chairDepartmentCode = assignment.DepartmentCode.Trim();
                departmentsByCode.TryGetValue(chairDepartmentCode, out var department);
                currentFacultyByIamId.TryGetValue(assignment.IamId.Trim(), out var faculty);
                return CreateAssignmentResponse(
                    active: IsRoleAssignmentActive(
                        isEligible: faculty != null,
                        targetIsActive: (department?.IsActive ?? false) &&
                            string.Equals(
                                faculty?.ResolvedReportingDepartmentCode?.Trim(),
                                chairDepartmentCode,
                                StringComparison.OrdinalIgnoreCase),
                        startDate: assignment.EffectiveStartDate,
                        endDate: assignment.EffectiveEndDateExclusive,
                        today: today,
                        closedUtc: assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: chairDepartmentCode,
                    targetName: department?.DepartmentName ?? chairDepartmentCode,
                    type: "chair",
                    employeesByIamId: employeesByIamId);
            }))
            .OrderByDescending(assignment => assignment.Active)
            .ThenBy(assignment => assignment.Type)
            .ThenBy(assignment => assignment.TargetName)
            .ThenBy(assignment => assignment.Name)
            .ToList();

        return new AdminRolesResponse(
            Assignments: assignments,
            Clusters: clusters.Select(cluster => new AdminRoleOption(cluster.Id.ToString(), cluster.ClusterName, cluster.IsActive)).ToList(),
            Departments: departments.Select(department => new AdminRoleOption(department.DepartmentCode, department.DepartmentName, department.IsActive)).ToList(),
            Users: BuildUserOptions(roleOptionsData));
    }

    private static IReadOnlyList<AdminRoleUserOption> BuildUserOptions(AdminRoleOptionsData roleOptionsData)
    {
        var currentFacultyByIamId = roleOptionsData.CurrentFaculty
            .ToDictionary(employee => employee.IamId.Trim(), StringComparer.OrdinalIgnoreCase);
        var departmentsByCode = roleOptionsData.Departments
            .ToDictionary(department => department.DepartmentCode, StringComparer.OrdinalIgnoreCase);
        return roleOptionsData.Employees
            .Select(employee =>
            {
                var iamId = employee.IamId.Trim();
                currentFacultyByIamId.TryGetValue(iamId, out var faculty);
                var departmentCode = NullIfWhiteSpace(faculty?.ResolvedReportingDepartmentCode);
                var departmentName = NullIfWhiteSpace(faculty?.ResolvedReportingDepartmentName);
                var departmentOptions = BuildDepartmentOptions(departmentCode, departmentName, departmentsByCode);

                return new AdminRoleUserOption(
                    DepartmentId: departmentCode,
                    DepartmentName: departmentName,
                    DepartmentOptions: departmentOptions,
                    Email: NullIfWhiteSpace(employee.Email) ?? string.Empty,
                    IamId: iamId,
                    Name: NullIfWhiteSpace(employee.DisplayName) ?? iamId);
            })
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.IamId, StringComparer.OrdinalIgnoreCase)
            .ToList();

    }

    private static AdminRoleAssignmentResponse CreateAssignmentResponse(
        bool active,
        string? effectiveEndDate,
        string? effectiveStartDate,
        string id,
        string iamId,
        string? targetId,
        string? targetName,
        string type,
        IReadOnlyDictionary<string, DirectoryEmployee> employeesByIamId)
    {
        var trimmedIamId = iamId.Trim();
        employeesByIamId.TryGetValue(trimmedIamId, out var employee);

        return new AdminRoleAssignmentResponse(
            Active: active,
            EffectiveEndDate: effectiveEndDate,
            EffectiveStartDate: effectiveStartDate,
            Email: NullIfWhiteSpace(employee?.Email) ?? string.Empty,
            Id: id,
            IamId: trimmedIamId,
            Name: NullIfWhiteSpace(employee?.DisplayName) ?? trimmedIamId,
            TargetId: targetId,
            TargetName: targetName,
            Type: type);
    }

    private static bool IsRoleAssignmentActive(
        bool isEligible,
        bool targetIsActive = true,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        DateOnly? today = null,
        DateTime? closedUtc = null)
    {
        if (!isEligible || !targetIsActive)
        {
            return false;
        }

        if (!startDate.HasValue || !today.HasValue)
        {
            return true;
        }

        return closedUtc == null && startDate.Value <= today.Value && (!endDate.HasValue || endDate.Value > today.Value);
    }

    private static DateOnly? GetEffectiveEndDateExclusive(DateOnly? currentValue, DateOnly today)
    {
        if (!currentValue.HasValue || currentValue.Value > today)
        {
            return today;
        }

        return currentValue;
    }

    private static IReadOnlyList<AdminRoleOption> BuildDepartmentOptions(
        string? departmentCode,
        string? departmentName,
        IReadOnlyDictionary<string, Department> departmentsByCode)
    {
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return [];
        }

        var normalizedDepartmentCode = departmentCode.Trim();

        return
        [
            new AdminRoleOption(
                normalizedDepartmentCode,
                NullIfWhiteSpace(departmentName)
                    ?? (departmentsByCode.TryGetValue(normalizedDepartmentCode, out var department)
                        ? department.DepartmentName
                        : normalizedDepartmentCode),
                departmentsByCode.TryGetValue(normalizedDepartmentCode, out var activeDepartment)
                    ? activeDepartment.IsActive
                    : false),
        ];
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

internal sealed record InactiveRoleAssignmentChanges(
    IReadOnlyList<AppAdminAssignment> AdminAssignmentsToDelete,
    IReadOnlyList<ClusterCaoAssignment> CaoAssignmentsToClose,
    IReadOnlyList<DepartmentChairAssignment> ChairAssignmentsToClose);

public sealed record AdminRolesResponse(
    IReadOnlyList<AdminRoleAssignmentResponse> Assignments,
    IReadOnlyList<AdminRoleOption> Clusters,
    IReadOnlyList<AdminRoleOption> Departments,
    IReadOnlyList<AdminRoleUserOption> Users);

public sealed record AdminRoleAssignmentResponse(
    bool Active,
    string? EffectiveEndDate,
    string? EffectiveStartDate,
    string Email,
    string Id,
    string IamId,
    string Name,
    string? TargetId,
    string? TargetName,
    string Type);

public sealed record AdminRoleOption(string Id, string Name, bool Active);

public sealed record AdminRoleUserOption(
    string? DepartmentId,
    string? DepartmentName,
    IReadOnlyList<AdminRoleOption> DepartmentOptions,
    string Email,
    string IamId,
    string Name);
