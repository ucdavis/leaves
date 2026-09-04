using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminDirectoryService
{
    private readonly AdminDirectoryDataService _directoryDataService;

    public AdminDirectoryService(AdminDirectoryDataService directoryDataService)
    {
        _directoryDataService = directoryDataService;
    }

    public async Task<AdminDepartmentsResponse> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryDataService.LoadDirectoryDataAsync(cancellationToken);
        return BuildDepartmentsResponse(directoryData);
    }

    public async Task<AdminFacultyResponse> GetFacultyAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryDataService.LoadDirectoryDataAsync(cancellationToken);
        return BuildFacultyResponse(directoryData);
    }

    internal static AdminFacultyResponse BuildFacultyResponse(AdminDirectoryData directoryData)
    {
        var departmentResponse = BuildDepartmentsResponse(directoryData);
        var currentCaoIamIds = directoryData.CurrentCaoAssignmentsByCluster.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var facultyIamIds = directoryData.CurrentEmployees
            .Where(employee => employee.HasCurrentAccrualRecord)
            .Where(employee =>
                !directoryData.NonFacultyIamIds.Contains(NormalizeKey(employee.IamId)) &&
                !currentCaoIamIds.Contains(NormalizeKey(employee.IamId)))
            .Select(employee => NormalizeKey(employee.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AdminFacultyResponse(
            Departments: departmentResponse.Departments,
            FacultyUsers: departmentResponse.Users
                .Where(user => facultyIamIds.Contains(NormalizeKey(user.IamId)))
                .ToList());
    }

    internal static AdminDepartmentsResponse BuildDepartmentsResponse(AdminDirectoryData directoryData)
    {
        var userIdByIamId = BuildUserIdByIamId(directoryData);
        var roleAssignments = BuildRoleAssignments(directoryData);

        return new AdminDepartmentsResponse(
            Clusters: BuildClusterResponses(directoryData, userIdByIamId),
            Departments: BuildDepartmentResponses(directoryData, userIdByIamId),
            Users: BuildUserResponses(directoryData, roleAssignments));
    }

    private static Dictionary<string, string> BuildUserIdByIamId(AdminDirectoryData directoryData)
    {
        var userIdByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().IamId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var employee in directoryData.CurrentEmployees)
        {
            var key = NormalizeKey(employee.IamId);
            if (!userIdByIamId.ContainsKey(key))
            {
                userIdByIamId[key] = employee.IamId.Trim();
            }
        }

        return userIdByIamId;
    }

    private static IReadOnlyList<AdminDepartmentResponse> BuildDepartmentResponses(
        AdminDirectoryData directoryData,
        IReadOnlyDictionary<string, string> userIdByIamId)
    {
        return directoryData.Departments
            .Select(department =>
            {
                directoryData.CurrentChairAssignmentsByDepartment.TryGetValue(
                    department.DepartmentCode.Trim(),
                    out var chairAssignment);
                var chairUserId = chairAssignment == null
                    ? null
                    : userIdByIamId.GetValueOrDefault(NormalizeKey(chairAssignment.IamId));

                return new AdminDepartmentResponse(
                    ApprovalMode: department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "notification",
                    ChairUserId: chairUserId,
                    ClusterId: department.ClusterId?.ToString(),
                    Code: department.DepartmentCode,
                    Id: department.DepartmentCode,
                    Name: department.DepartmentName,
                    RoutingEmails: department.DepartmentEmailRoutings
                        .Where(routing => routing.IsActive)
                        .OrderBy(routing => routing.ToEmail)
                        .Select(routing => new DepartmentRoutingEmailResponse(
                            Address: routing.ToEmail,
                            Id: routing.Id.ToString(),
                            Kind: "to"))
                        .ToList());
            })
            .ToList();
    }

    private static IReadOnlyList<AdminClusterResponse> BuildClusterResponses(
        AdminDirectoryData directoryData,
        IReadOnlyDictionary<string, string> userIdByIamId)
    {
        return directoryData.Clusters
            .Select(cluster =>
            {
                directoryData.CurrentCaoAssignmentsByCluster.TryGetValue(cluster.Id, out var caoAssignment);
                var caoUserId = caoAssignment == null
                    ? null
                    : userIdByIamId.GetValueOrDefault(NormalizeKey(caoAssignment.IamId));

                return new AdminClusterResponse(
                    CaoUserId: caoUserId,
                    Id: cluster.Id.ToString(),
                    Name: cluster.ClusterName);
            })
            .ToList();
    }

    private static RoleAssignments BuildRoleAssignments(AdminDirectoryData directoryData)
    {
        var chairIamIds = directoryData.CurrentChairAssignmentsByDepartment.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var caoIamIds = directoryData.CurrentCaoAssignmentsByCluster.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new RoleAssignments(
            AdminIamIds: directoryData.AdminIamIds,
            ChairIamIds: chairIamIds,
            CaoIamIds: caoIamIds);
    }

    private static IReadOnlyList<AdminUserResponse> BuildUserResponses(
        AdminDirectoryData directoryData,
        RoleAssignments roleAssignments)
    {
        var appUsersByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return directoryData.CurrentEmployees
            .Select(employee =>
            {
                var lookupIamId = NormalizeKey(employee.IamId);
                appUsersByIamId.TryGetValue(lookupIamId, out var appUser);

                EmployeeReportingDepartmentOverride? currentOverride = null;
                if (employee.ReportingDepartmentOverrideId.HasValue)
                {
                    directoryData.CurrentOverridesById.TryGetValue(
                        employee.ReportingDepartmentOverrideId.Value,
                        out currentOverride);
                }

                var role = GetRole(
                    roleAssignments.AdminIamIds.Contains(lookupIamId),
                    roleAssignments.ChairIamIds.Contains(lookupIamId),
                    roleAssignments.CaoIamIds.Contains(lookupIamId));
                var iamId = employee.IamId.Trim();

                return new AdminUserResponse(
                    Id: iamId,
                    Active: appUser?.IsActive ?? true,
                    DepartmentId: NullIfWhiteSpace(employee.ResolvedReportingDepartmentCode),
                    DepartmentOverrideEndDate: currentOverride?.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    DepartmentOverrideId: NullIfWhiteSpace(currentOverride?.DepartmentCode),
                    DepartmentOverrideStartDate: currentOverride?.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    Designation: GetDesignation(role, directoryData.NonFacultyIamIds.Contains(lookupIamId)),
                    Email: NullIfWhiteSpace(employee.Email) ?? string.Empty,
                    EmployeeId: NullIfWhiteSpace(employee.EmployeeId) ?? string.Empty,
                    HasAppUser: appUser != null,
                    IamId: iamId,
                    Name: NullIfWhiteSpace(employee.DisplayName) ?? NullIfWhiteSpace(appUser?.DisplayName) ?? iamId,
                    Position: NullIfWhiteSpace(employee.JobCodeDescription) ?? string.Empty,
                    Role: role);
            })
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.IamId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetRole(bool isAdmin, bool isChair, bool isCao)
    {
        if (isAdmin)
        {
            return "admin";
        }

        if (isCao)
        {
            return "cao";
        }

        return isChair ? "chair" : "faculty";
    }

    private static string GetDesignation(string role, bool isNonFaculty)
    {
        if (role is "admin" or "cao" or "chair")
        {
            return role;
        }

        return isNonFaculty ? "nfa" : "faculty";
    }

    internal static string NormalizeKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public sealed record AdminDepartmentsResponse(
    IReadOnlyList<AdminClusterResponse> Clusters,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> Users);

public sealed record AdminFacultyResponse(
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> FacultyUsers);

public sealed record AdminClusterResponse(string? CaoUserId, string Id, string Name);

public sealed record AdminDepartmentResponse(
    string ApprovalMode,
    string? ChairUserId,
    string? ClusterId,
    string Code,
    string Id,
    string Name,
    IReadOnlyList<DepartmentRoutingEmailResponse> RoutingEmails);

public sealed record DepartmentRoutingEmailResponse(string Address, string Id, string Kind);

public sealed record AdminUserResponse(
    string Id,
    bool Active,
    string? DepartmentId,
    string? DepartmentOverrideEndDate,
    string? DepartmentOverrideId,
    string? DepartmentOverrideStartDate,
    string Designation,
    string Email,
    string EmployeeId,
    bool HasAppUser,
    string IamId,
    string Name,
    string Position,
    string Role);

public sealed record RoleAssignments(
    IReadOnlySet<string> AdminIamIds,
    IReadOnlySet<string> ChairIamIds,
    IReadOnlySet<string> CaoIamIds);
