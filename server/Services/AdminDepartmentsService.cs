using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminDepartmentsService
{
    private readonly AdminDirectoryService _directoryService;

    public AdminDepartmentsService(AdminDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    public async Task<AdminDepartmentsResponse> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryService.LoadDirectoryDataAsync(cancellationToken);
        return BuildDepartmentsResponse(directoryData);
    }

    internal static AdminDepartmentsResponse BuildDepartmentsResponse(AdminDirectoryData directoryData)
    {
        var userIdByIamId = BuildUserIdByIamId(directoryData);

        var departmentResponses = BuildDepartmentResponses(
            directoryData.Departments,
            directoryData.CurrentChairAssignmentsByDepartment,
            userIdByIamId);
        var clusterResponses = BuildClusterResponses(
            directoryData.Clusters,
            directoryData.CurrentCaoAssignmentsByCluster,
            userIdByIamId);
        var roleAssignments = BuildRoleAssignments(
            directoryData.AdminIamIdSet,
            directoryData.CurrentChairAssignmentsByDepartment,
            directoryData.CurrentCaoAssignmentsByCluster);
        var userResponses = BuildUserResponses(directoryData, roleAssignments);

        return new AdminDepartmentsResponse(
            Clusters: clusterResponses,
            Departments: departmentResponses,
            Users: userResponses);
    }

    internal static Dictionary<string, string> BuildUserIdByIamId(AdminDirectoryData directoryData)
    {
        var userIdByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().IamId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var person in directoryData.People)
        {
            var key = NormalizeKey(person.IamId);
            if (!userIdByIamId.ContainsKey(key))
            {
                userIdByIamId[key] = person.IamId.Trim();
            }
        }

        return userIdByIamId;
    }

    internal static IReadOnlyList<AdminDepartmentResponse> BuildDepartmentResponses(
        IEnumerable<Department> departments,
        IReadOnlyDictionary<string, DepartmentChairAssignment> currentChairAssignmentsByDepartment,
        IReadOnlyDictionary<string, string> userIdByIamId)
    {
        return departments
            .Select(department =>
            {
                currentChairAssignmentsByDepartment.TryGetValue(department.DepartmentCode.Trim(), out var chairAssignment);
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

    internal static IReadOnlyList<AdminClusterResponse> BuildClusterResponses(
        IEnumerable<Cluster> clusters,
        IReadOnlyDictionary<int, ClusterCaoAssignment> currentCaoAssignmentsByCluster,
        IReadOnlyDictionary<string, string> userIdByIamId)
    {
        return clusters
            .Select(cluster =>
            {
                currentCaoAssignmentsByCluster.TryGetValue(cluster.Id, out var caoAssignment);
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

    internal static RoleAssignments BuildRoleAssignments(
        IReadOnlySet<string> adminIamIdSet,
        IReadOnlyDictionary<string, DepartmentChairAssignment> currentChairAssignmentsByDepartment,
        IReadOnlyDictionary<int, ClusterCaoAssignment> currentCaoAssignmentsByCluster)
    {
        var chairIamIds = currentChairAssignmentsByDepartment.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var caoIamIds = currentCaoAssignmentsByCluster.Values
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new RoleAssignments(
            AdminIamIds: adminIamIdSet,
            ChairIamIds: chairIamIds,
            CaoIamIds: caoIamIds);
    }

    internal static IReadOnlyList<AdminUserResponse> BuildUserResponses(
        AdminDirectoryData directoryData,
        RoleAssignments roleAssignments)
    {
        var peopleByIamId = directoryData.People
            .GroupBy(person => NormalizeKey(person.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var appUsersByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return peopleByIamId.Keys
            .OrderBy(key =>
                peopleByIamId.TryGetValue(key, out var person)
                    ? person.FullName ?? person.IamId
                    : key,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(lookupIamId =>
            {
                appUsersByIamId.TryGetValue(lookupIamId, out var appUser);
                peopleByIamId.TryGetValue(lookupIamId, out var person);

                var iamId = person?.IamId.Trim() ?? lookupIamId;
                var employeeId = NormalizeEmployeeId(appUser?.EmployeeId)
                    ?? NormalizeEmployeeId(person?.EmployeeId);
                directoryData.LatestAccrualByEmployeeId.TryGetValue(employeeId ?? string.Empty, out var latestAccrual);
                directoryData.CurrentOverridesByIamId.TryGetValue(lookupIamId, out var currentOverride);

                var departmentCode = currentOverride?.DepartmentCode.Trim()
                    ?? latestAccrual?.Level5Dept.Trim();
                var role = GetRole(
                    roleAssignments.AdminIamIds.Contains(lookupIamId),
                    roleAssignments.ChairIamIds.Contains(lookupIamId),
                    roleAssignments.CaoIamIds.Contains(lookupIamId));

                return new AdminUserResponse(
                    Id: iamId,
                    Active: appUser?.IsActive ?? true,
                    DepartmentId: departmentCode,
                    DepartmentOverrideEndDate: currentOverride?.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    DepartmentOverrideId: currentOverride?.DepartmentCode,
                    DepartmentOverrideStartDate: currentOverride?.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    Designation: GetDesignation(role, person),
                    Email: appUser?.Email ?? person?.Email ?? string.Empty,
                    EmployeeId: employeeId ?? string.Empty,
                    HasAppUser: appUser != null,
                    IamId: iamId,
                    Name: appUser?.DisplayName ?? person?.FullName ?? iamId,
                    Position: latestAccrual?.JobCodeDescription ?? string.Empty,
                    Role: role);
            })
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

    private static string GetDesignation(string role, Person? person)
    {
        if (role is "admin" or "cao" or "chair")
        {
            return role;
        }

        if (person?.IsFaculty == false)
        {
            return "nfa";
        }

        return "faculty";
    }

    internal static string NormalizeKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    internal static string? NormalizeEmployeeId(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public sealed record AdminDepartmentsResponse(
    IReadOnlyList<AdminClusterResponse> Clusters,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> Users);

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
