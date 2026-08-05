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
        var roleOptionsData = await _directoryDataService.LoadRoleOptionsDataAsync(cancellationToken);
        var roleAssignmentsData = await _directoryDataService.LoadRoleAssignmentsDataAsync(cancellationToken);

        return BuildRolesResponse(roleOptionsData, roleAssignmentsData, today);
    }

    internal static AdminRolesResponse BuildRolesResponse(
        AdminRoleOptionsData roleOptionsData,
        AdminRoleAssignmentsData roleAssignmentsData,
        DateOnly today)
    {
        var appUsersByIamId = roleOptionsData.AppUsers
            .GroupBy(user => user.IamId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var departments = roleOptionsData.Departments;
        var departmentsByCode = departments.ToDictionary(department => department.DepartmentCode, StringComparer.OrdinalIgnoreCase);
        var clusters = roleOptionsData.Clusters;
        var clustersById = clusters.ToDictionary(cluster => cluster.Id);

        var assignments = roleAssignmentsData.AdminAssignments
            .Select(assignment => CreateAssignmentResponse(
                active: true,
                effectiveEndDate: null,
                effectiveStartDate: null,
                id: assignment.Id.ToString(),
                iamId: assignment.IamId,
                targetId: null,
                targetName: null,
                type: "admin",
                appUsersByIamId: appUsersByIamId))
            .Concat(roleAssignmentsData.CaoAssignments.Select(assignment =>
            {
                clustersById.TryGetValue(assignment.ClusterId, out var cluster);
                return CreateAssignmentResponse(
                    active: IsActive(assignment.EffectiveStartDate, assignment.EffectiveEndDateExclusive, today, assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: assignment.ClusterId.ToString(),
                    targetName: cluster?.ClusterName ?? $"Cluster {assignment.ClusterId}",
                    type: "cao",
                    appUsersByIamId: appUsersByIamId);
            }))
            .Concat(roleAssignmentsData.ChairAssignments.Select(assignment =>
            {
                departmentsByCode.TryGetValue(assignment.DepartmentCode, out var department);
                return CreateAssignmentResponse(
                    active: IsActive(assignment.EffectiveStartDate, assignment.EffectiveEndDateExclusive, today, assignment.ClosedUtc),
                    effectiveEndDate: assignment.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    effectiveStartDate: assignment.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    id: assignment.Id.ToString(),
                    iamId: assignment.IamId,
                    targetId: assignment.DepartmentCode,
                    targetName: department?.DepartmentName ?? assignment.DepartmentCode,
                    type: "chair",
                    appUsersByIamId: appUsersByIamId);
            }))
            .OrderByDescending(assignment => assignment.Active)
            .ThenBy(assignment => assignment.Type)
            .ThenBy(assignment => assignment.TargetName)
            .ThenBy(assignment => assignment.Name)
            .ToList();

        var users = roleOptionsData.CurrentEmployees
            .Where(employee => employee.HasCurrentAccrualRecord)
            .Select(employee =>
            {
                var iamId = employee.IamId.Trim();
                appUsersByIamId.TryGetValue(iamId, out var appUser);
                var departmentCode = NullIfWhiteSpace(employee.ResolvedReportingDepartmentCode);
                var departmentName = NullIfWhiteSpace(employee.ResolvedReportingDepartmentName);
                var departmentOptions = BuildDepartmentOptions(departmentCode, departmentName, departmentsByCode);

                return new AdminRoleUserOption(
                    DepartmentId: departmentCode,
                    DepartmentName: departmentName,
                    DepartmentOptions: departmentOptions,
                    Email: NullIfWhiteSpace(appUser?.Email) ?? NullIfWhiteSpace(employee.Email) ?? string.Empty,
                    IamId: iamId,
                    Name: NullIfWhiteSpace(appUser?.DisplayName) ?? NullIfWhiteSpace(employee.DisplayName) ?? iamId);
            })
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.IamId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AdminRolesResponse(
            Assignments: assignments,
            Clusters: clusters.Select(cluster => new AdminRoleOption(cluster.Id.ToString(), cluster.ClusterName)).ToList(),
            Departments: departments.Select(department => new AdminRoleOption(department.DepartmentCode, department.DepartmentName)).ToList(),
            Users: users);
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
        IReadOnlyDictionary<string, AppUser> appUsersByIamId)
    {
        var trimmedIamId = iamId.Trim();
        appUsersByIamId.TryGetValue(trimmedIamId, out var appUser);

        return new AdminRoleAssignmentResponse(
            Active: active,
            EffectiveEndDate: effectiveEndDate,
            EffectiveStartDate: effectiveStartDate,
            Email: appUser?.Email ?? string.Empty,
            Id: id,
            IamId: trimmedIamId,
            Name: appUser?.DisplayName ?? trimmedIamId,
            TargetId: targetId,
            TargetName: targetName,
            Type: type);
    }

    private static bool IsActive(DateOnly startDate, DateOnly? endDate, DateOnly today, DateTime? closedUtc)
    {
        return closedUtc == null && startDate <= today && (!endDate.HasValue || endDate.Value > today);
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
                        : normalizedDepartmentCode)),
        ];
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

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

public sealed record AdminRoleOption(string Id, string Name);

public sealed record AdminRoleUserOption(
    string? DepartmentId,
    string? DepartmentName,
    IReadOnlyList<AdminRoleOption> DepartmentOptions,
    string Email,
    string IamId,
    string Name);
