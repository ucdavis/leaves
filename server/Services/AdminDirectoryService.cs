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
        var summaryData = await _directoryDataService.LoadDepartmentSummaryDataAsync(cancellationToken);
        return BuildDepartmentSummaryResponse(summaryData);
    }

    public async Task<AdminDepartmentRosterResponse> GetDepartmentRosterAsync(
        string departmentCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rosterData = await _directoryDataService.LoadDepartmentRosterDataAsync(
            departmentCode,
            page,
            pageSize,
            cancellationToken);
        var chairIamIds = rosterData.CurrentChairAssignments
            .Select(assignment => NormalizeKey(assignment.IamId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directoryData = new AdminDirectoryData(
            AppUsers: rosterData.AppUsers,
            Clusters: [],
            CurrentCaoAssignmentsByCluster: new Dictionary<int, ClusterCaoAssignment>(),
            CurrentChairAssignmentsByDepartment: new Dictionary<string, DepartmentChairAssignment>(),
            CurrentEmployees: rosterData.CurrentEmployees,
            CurrentOverridesById: rosterData.CurrentOverridesById,
            Departments: [],
            AdminIamIds: rosterData.AdminIamIds,
            NonFacultyIamIds: rosterData.NonFacultyIamIds);
        var users = BuildUserResponses(
            directoryData,
            new RoleAssignments(
                AdminIamIds: rosterData.AdminIamIds,
                ChairIamIds: chairIamIds,
                CaoIamIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        return new AdminDepartmentRosterResponse(rosterData.TotalCount, users);
    }

    public async Task<AdminFacultyResponse> GetFacultyAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryDataService.LoadFacultyDirectoryDataAsync(cancellationToken);
        return BuildFacultyResponse(directoryData);
    }

    internal static AdminFacultyResponse BuildFacultyResponse(AdminDirectoryData directoryData)
    {
        var departments = BuildDepartmentResponses(directoryData);
        var users = BuildUserResponses(directoryData, BuildRoleAssignments(directoryData));
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
            Departments: departments,
            FacultyUsers: users
                .Where(user => facultyIamIds.Contains(NormalizeKey(user.IamId)))
                .ToList());
    }

    private static AdminDepartmentsResponse BuildDepartmentSummaryResponse(AdminDepartmentSummaryData summaryData)
    {
        var departments = summaryData.Departments
            .Select(department =>
            {
                summaryData.CurrentChairAssignmentsByDepartment.TryGetValue(
                    department.DepartmentCode.Trim(),
                    out var chairAssignment);
                var chairIamId = chairAssignment?.IamId.Trim();

                return new AdminDepartmentResponse(
                    ApprovalMode: department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "notification",
                    ChairUserId: chairIamId,
                    ChairUserName: DisplayNameFor(summaryData.AssignmentNamesByIamId, chairIamId),
                    ClusterId: department.ClusterId?.ToString(),
                    Code: department.DepartmentCode,
                    Id: department.DepartmentCode,
                    LinkedUserCount: summaryData.LinkedUserCountsByDepartment.GetValueOrDefault(department.DepartmentCode.Trim()),
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
        var clusters = summaryData.Clusters
            .Select(cluster =>
            {
                summaryData.CurrentCaoAssignmentsByCluster.TryGetValue(cluster.Id, out var caoAssignment);
                var caoIamId = caoAssignment?.IamId.Trim();
                return new AdminClusterResponse(
                    CaoUserId: caoIamId,
                    CaoUserName: DisplayNameFor(summaryData.AssignmentNamesByIamId, caoIamId),
                    Id: cluster.Id.ToString(),
                    Name: cluster.ClusterName);
            })
            .ToList();

        return new AdminDepartmentsResponse(clusters, departments);
    }

    private static IReadOnlyList<AdminDepartmentResponse> BuildDepartmentResponses(
        AdminDirectoryData directoryData)
    {
        return directoryData.Departments
            .Select(department =>
            {
                directoryData.CurrentChairAssignmentsByDepartment.TryGetValue(
                    department.DepartmentCode.Trim(),
                    out var chairAssignment);
                var chairUserId = chairAssignment == null
                    ? null
                    : chairAssignment.IamId.Trim();

                return new AdminDepartmentResponse(
                    ApprovalMode: department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "notification",
                    ChairUserId: chairUserId,
                    ChairUserName: null,
                    ClusterId: department.ClusterId?.ToString(),
                    Code: department.DepartmentCode,
                    Id: department.DepartmentCode,
                    LinkedUserCount: 0,
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
        AdminDirectoryData directoryData)
    {
        return directoryData.Clusters
            .Select(cluster =>
            {
                directoryData.CurrentCaoAssignmentsByCluster.TryGetValue(cluster.Id, out var caoAssignment);
                var caoUserId = caoAssignment == null
                    ? null
                    : caoAssignment.IamId.Trim();

                return new AdminClusterResponse(
                    CaoUserId: caoUserId,
                    CaoUserName: null,
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

    private static string? DisplayNameFor(IReadOnlyDictionary<string, string> namesByIamId, string? iamId)
    {
        return string.IsNullOrWhiteSpace(iamId)
            ? null
            : namesByIamId.GetValueOrDefault(iamId);
    }
}

public sealed record AdminDepartmentsResponse(
    IReadOnlyList<AdminClusterResponse> Clusters,
    IReadOnlyList<AdminDepartmentResponse> Departments);

public sealed record AdminDepartmentRosterResponse(
    int TotalCount,
    IReadOnlyList<AdminUserResponse> Users);

public sealed record AdminFacultyResponse(
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> FacultyUsers);

public sealed record AdminClusterResponse(string? CaoUserId, string? CaoUserName, string Id, string Name);

public sealed record AdminDepartmentResponse(
    string ApprovalMode,
    string? ChairUserId,
    string? ChairUserName,
    string? ClusterId,
    string Code,
    string Id,
    int LinkedUserCount,
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
