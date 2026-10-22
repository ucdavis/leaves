using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminDirectoryService
{
    private readonly AdminDirectoryDataService _directoryDataService;

    public AdminDirectoryService(AdminDirectoryDataService directoryDataService)
    {
        _directoryDataService = directoryDataService;
    }

    public async Task<AdminDepartmentOverviewResponse> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var overviewData = await _directoryDataService.LoadDepartmentOverviewAsync(cancellationToken);
        return BuildDepartmentsResponse(overviewData);
    }

    public async Task<IReadOnlyList<DepartmentFacultyResponse>> GetDepartmentFacultyAsync(
        string departmentCode, CancellationToken cancellationToken)
    {
        var faculty = await _directoryDataService.LoadDepartmentFacultyAsync(departmentCode, cancellationToken);
        return faculty.Select(person => new DepartmentFacultyResponse(
                person.IamId,
                NullIfWhiteSpace(person.Name) ?? person.IamId,
                NullIfWhiteSpace(person.Email) ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminCaoUserResponse>> SearchCaoCandidatesAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var ids = await _directoryDataService.SearchEmployeeIdsAsync(query, forCao: true, cancellationToken);
        var employees = await _directoryDataService.LoadCaoEmployeesAsync(ids, cancellationToken);
        var idOrder = ids.Select((id, index) => (Id: id.Trim(), Index: index))
            .ToDictionary(item => item.Id, item => item.Index, StringComparer.OrdinalIgnoreCase);
        return employees.OrderBy(employee => idOrder[employee.IamId.Trim()])
            .Select(employee => new AdminCaoUserResponse(
                Id: employee.IamId.Trim(),
                Active: true,
                Designation: GetDesignation("faculty", employee.IsFaculty == false),
                Email: NullIfWhiteSpace(employee.Email) ?? string.Empty,
                Name: NullIfWhiteSpace(employee.DisplayName) ?? employee.IamId.Trim())).ToList();
    }

    public async Task<AdminFacultyResponse> GetFacultyAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryDataService.LoadDirectoryDataAsync(cancellationToken);
        return BuildFacultyResponse(directoryData);
    }

    public async Task<IReadOnlyList<FacultyWithOverrideResponse>> GetFacultyWithOverridesAsync(
        CancellationToken cancellationToken)
    {
        var employees = await _directoryDataService.LoadFacultyWithOverridesAsync(cancellationToken);
        var roleAssignments = await _directoryDataService.LoadCurrentRoleAssignmentIdsAsync(cancellationToken);

        return employees.Select(employee =>
            {
                var iamId = employee.IamId.Trim();
                var lookupIamId = NormalizeKey(iamId);
                var role = GetRole(
                    roleAssignments.AdminIamIds.Contains(lookupIamId),
                    roleAssignments.ChairIamIds.Contains(lookupIamId),
                    roleAssignments.CaoIamIds.Contains(lookupIamId));

                return new FacultyWithOverrideResponse(
                    Id: iamId,
                    Active: employee.AppUserIsActive ?? true,
                    DepartmentId: NullIfWhiteSpace(employee.DepartmentCode),
                    DepartmentOverrideEndDate: employee.EffectiveEndDateExclusive?.ToString("yyyy-MM-dd"),
                    DepartmentOverrideId: NullIfWhiteSpace(employee.DepartmentCode),
                    DepartmentOverrideStartDate: employee.EffectiveStartDate.ToString("yyyy-MM-dd"),
                    Email: NullIfWhiteSpace(employee.Email) ?? string.Empty,
                    EmployeeId: NullIfWhiteSpace(employee.EmployeeId) ?? string.Empty,
                    HasAppUser: employee.AppUserIsActive.HasValue,
                    IamId: iamId,
                    IsActiveInIam: employee.IsActiveInIam,
                    Name: NullIfWhiteSpace(employee.FullName) ??
                          NullIfWhiteSpace(employee.AppUserDisplayName) ?? iamId,
                    Position: string.Empty,
                    Role: role);
            })
            .ToList();
    }

    internal static AdminFacultyResponse BuildFacultyResponse(AdminDirectoryData directoryData)
    {
        return new AdminFacultyResponse(
            Departments: BuildFacultyDepartmentResponses(directoryData),
            FacultyUsers: BuildFacultyUserResponses(directoryData, BuildRoleAssignments(directoryData)));
    }

    private static IReadOnlyList<AdminDepartmentResponse> BuildFacultyDepartmentResponses(AdminDirectoryData data)
    {
        var counts = data.CurrentFaculty
            .Where(faculty => !string.IsNullOrWhiteSpace(faculty.ResolvedReportingDepartmentCode))
            .GroupBy(faculty => faculty.ResolvedReportingDepartmentCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(faculty => faculty.IamId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(), StringComparer.OrdinalIgnoreCase);

        return data.Departments.Select(department =>
        {
            data.CurrentChairAssignmentsByDepartment.TryGetValue(department.DepartmentCode.Trim(), out var chair);
            counts.TryGetValue(department.DepartmentCode.Trim(), out var count);
            return new AdminDepartmentResponse(
                department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "auto",
                chair?.IamId.Trim(), null, department.ClusterId?.ToString(), department.DepartmentCode,
                count, count > 0, department.DepartmentCode, department.DepartmentName,
                department.DepartmentEmailRoutings.Where(routing => routing.IsActive)
                    .OrderBy(routing => routing.ToEmail)
                    .Select(routing => new DepartmentRoutingEmailResponse(routing.ToEmail, routing.Id.ToString(), "to"))
                    .ToList());
        }).ToList();
    }

    internal static AdminDepartmentOverviewResponse BuildDepartmentsResponse(AdminDepartmentOverviewData data)
    {
        return new AdminDepartmentOverviewResponse(
            Clusters: BuildClusterResponses(data, isActive: true),
            Departments: BuildDepartmentResponses(data, isActive: true),
            InactiveClusters: BuildClusterResponses(data, isActive: false),
            InactiveDepartments: BuildDepartmentResponses(data, isActive: false));
    }

    private static IReadOnlyList<AdminDepartmentResponse> BuildDepartmentResponses(
        AdminDepartmentOverviewData data,
        bool? isActive = null)
    {
        return data.Departments
            .Where(department => !isActive.HasValue || department.IsActive == isActive.Value)
            .Select(department =>
            {
                data.CurrentChairAssignmentsByDepartment.TryGetValue(
                    department.DepartmentCode.Trim(),
                    out var chairAssignment);
                var chairUserId = chairAssignment?.IamId.Trim();
                data.FacultyCountsByDepartment.TryGetValue(department.DepartmentCode.Trim(), out var facultyCount);
                CaoDirectoryEmployee? chair = null;
                if (chairUserId != null)
                {
                    data.AssignedPeopleByIamId.TryGetValue(chairUserId, out chair);
                }

                return new AdminDepartmentResponse(
                    ApprovalMode: department.WorkflowMode == WorkflowMode.ApprovalRequired ? "approval" : "auto",
                    ChairUserId: chairUserId,
                    ChairName: chairUserId == null ? null : NullIfWhiteSpace(chair?.DisplayName) ?? chairUserId,
                    ClusterId: department.ClusterId?.ToString(),
                    Code: department.DepartmentCode,
                    FacultyCount: facultyCount,
                    HasFaculty: facultyCount > 0,
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
        AdminDepartmentOverviewData data,
        bool? isActive = null)
    {
        return data.Clusters
            .Where(cluster => !isActive.HasValue || cluster.IsActive == isActive.Value)
            .Select(cluster =>
            {
                data.CurrentCaoAssignmentsByCluster.TryGetValue(cluster.Id, out var caoAssignment);
                var caoUserId = caoAssignment?.IamId.Trim();
                CaoDirectoryEmployee? cao = null;
                if (caoUserId != null)
                {
                    data.AssignedPeopleByIamId.TryGetValue(caoUserId, out cao);
                }

                return new AdminClusterResponse(
                    CaoUserId: caoUserId,
                    CaoName: caoUserId == null ? null : NullIfWhiteSpace(cao?.DisplayName) ?? caoUserId,
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

    private static IReadOnlyList<AdminUserResponse> BuildFacultyUserResponses(
        AdminDirectoryData directoryData,
        RoleAssignments roleAssignments)
    {
        var appUsersByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return directoryData.CurrentFaculty
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
                    Designation: GetDesignation(role, isNonFaculty: false),
                    Email: NullIfWhiteSpace(employee.Email) ?? string.Empty,
                    EmployeeId: NullIfWhiteSpace(employee.EmployeeId) ?? string.Empty,
                    HasAppUser: appUser != null,
                    IamId: iamId,
                    IsActiveInIam: employee.IsActiveInIam,
                    Name: NullIfWhiteSpace(employee.DisplayName) ?? NullIfWhiteSpace(appUser?.DisplayName) ?? iamId,
                    Position: NullIfWhiteSpace(employee.JobCodeDescription) ?? string.Empty,
                    Role: role);
            })
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.IamId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AdminCaoUserResponse> BuildCaoUserResponses(
        AdminDirectoryData directoryData,
        IReadOnlyList<CaoDirectoryEmployee> employees,
        RoleAssignments roleAssignments)
    {
        var appUsersByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return employees.Select(employee =>
            {
                var iamId = employee.IamId.Trim();
                var lookupIamId = NormalizeKey(iamId);
                appUsersByIamId.TryGetValue(lookupIamId, out var appUser);
                var role = GetRole(
                    roleAssignments.AdminIamIds.Contains(lookupIamId),
                    roleAssignments.ChairIamIds.Contains(lookupIamId),
                    roleAssignments.CaoIamIds.Contains(lookupIamId));

                return new AdminCaoUserResponse(
                    Id: iamId,
                    Active: appUser?.IsActive ?? true,
                    Designation: GetDesignation(role, employee.IsFaculty == false),
                    Email: NullIfWhiteSpace(employee.Email) ?? string.Empty,
                    Name: NullIfWhiteSpace(employee.DisplayName) ?? NullIfWhiteSpace(appUser?.DisplayName) ?? iamId);
            })
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Id, StringComparer.OrdinalIgnoreCase)
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

public sealed record AdminDepartmentOverviewResponse(
    IReadOnlyList<AdminClusterResponse> Clusters,
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminClusterResponse> InactiveClusters,
    IReadOnlyList<AdminDepartmentResponse> InactiveDepartments);

public sealed record DepartmentFacultyResponse(string Id, string Name, string Email);

public sealed record AdminCaoUserResponse(string Id, bool Active, string Designation, string Email, string Name);

public sealed record AdminFacultyResponse(
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> FacultyUsers);

public sealed record FacultyWithOverrideResponse(
    string Id,
    bool Active,
    string? DepartmentId,
    string? DepartmentOverrideEndDate,
    string? DepartmentOverrideId,
    string? DepartmentOverrideStartDate,
    string Email,
    string EmployeeId,
    bool HasAppUser,
    string IamId,
    bool IsActiveInIam,
    string Name,
    string Position,
    string Role);

public sealed record AdminClusterResponse(string? CaoUserId, string? CaoName, string Id, string Name);

public sealed record AdminDepartmentResponse(
    string ApprovalMode,
    string? ChairUserId,
    string? ChairName,
    string? ClusterId,
    string Code,
    int FacultyCount,
    bool HasFaculty,
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
    bool IsActiveInIam,
    string Name,
    string Position,
    string Role);

public sealed record RoleAssignments(
    IReadOnlySet<string> AdminIamIds,
    IReadOnlySet<string> ChairIamIds,
    IReadOnlySet<string> CaoIamIds);
