namespace Server.Services;

public sealed class AdminFacultyService
{
    private readonly AdminDirectoryService _directoryService;

    public AdminFacultyService(AdminDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    public async Task<AdminFacultyResponse> GetFacultyAsync(CancellationToken cancellationToken)
    {
        var directoryData = await _directoryService.LoadDirectoryDataAsync(cancellationToken);
        var userIdByIamId = BuildUserIdByIamId(directoryData);
        var departments = BuildDepartmentResponses(
            directoryData.Departments,
            directoryData.CurrentChairAssignmentsByDepartment,
            userIdByIamId);
        var roleAssignments = BuildRoleAssignments(
            directoryData.AdminIamIdSet,
            directoryData.CurrentChairAssignmentsByDepartment,
            directoryData.CurrentCaoAssignmentsByCluster);
        var users = BuildUserResponses(directoryData, roleAssignments);
        var facultyUsers = BuildFacultyUserResponses(directoryData, users);

        return new AdminFacultyResponse(
            Departments: departments,
            FacultyUsers: facultyUsers);
    }

    private static Dictionary<string, string> BuildUserIdByIamId(AdminDirectoryData directoryData)
    {
        var userIdByIamId = directoryData.AppUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
            .GroupBy(user => NormalizeKey(user.IamId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().IamId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var person in directoryData.People)
        {
            var key = NormalizeKey(person.IamId);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!userIdByIamId.ContainsKey(key))
            {
                userIdByIamId[key] = person.IamId?.Trim() ?? string.Empty;
            }
        }

        return userIdByIamId;
    }

    private static IReadOnlyList<AdminDepartmentResponse> BuildDepartmentResponses(
        IEnumerable<Server.Core.Domain.Department> departments,
        IReadOnlyDictionary<string, Server.Core.Domain.DepartmentChairAssignment> currentChairAssignmentsByDepartment,
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
                    ApprovalMode: department.WorkflowMode == Server.Core.Domain.WorkflowMode.ApprovalRequired ? "approval" : "notification",
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

    private static RoleAssignments BuildRoleAssignments(
        IReadOnlySet<string> adminIamIdSet,
        IReadOnlyDictionary<string, Server.Core.Domain.DepartmentChairAssignment> currentChairAssignmentsByDepartment,
        IReadOnlyDictionary<int, Server.Core.Domain.ClusterCaoAssignment> currentCaoAssignmentsByCluster)
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

    private static IReadOnlyList<AdminUserResponse> BuildUserResponses(
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

                var iamId = person?.IamId?.Trim() ?? lookupIamId;
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

    private static IReadOnlyList<AdminUserResponse> BuildFacultyUserResponses(
        AdminDirectoryData directoryData,
        IReadOnlyList<AdminUserResponse> allUsers)
    {
        var usersByEmployeeId = allUsers
            .Where(user => !string.IsNullOrWhiteSpace(user.EmployeeId))
            .GroupBy(user => NormalizeEmployeeId(user.EmployeeId)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return directoryData.LatestAccrualByEmployeeId
            .OrderBy(item => item.Value.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var employeeId = item.Key;
                var latestAccrual = item.Value;
                usersByEmployeeId.TryGetValue(employeeId, out var matchedUser);

                return new AdminUserResponse(
                    Id: matchedUser?.Id ?? employeeId,
                    Active: matchedUser?.Active ?? true,
                    DepartmentId: matchedUser?.DepartmentId ?? latestAccrual.Level5Dept.Trim(),
                    DepartmentOverrideEndDate: matchedUser?.DepartmentOverrideEndDate,
                    DepartmentOverrideId: matchedUser?.DepartmentOverrideId,
                    DepartmentOverrideStartDate: matchedUser?.DepartmentOverrideStartDate,
                    Designation: matchedUser?.Designation ?? "faculty",
                    Email: matchedUser?.Email ?? latestAccrual.EmployeeEmail ?? string.Empty,
                    EmployeeId: employeeId,
                    HasAppUser: matchedUser?.HasAppUser ?? false,
                    IamId: matchedUser?.IamId ?? string.Empty,
                    Name: matchedUser?.Name ?? latestAccrual.EmployeeName,
                    Position: latestAccrual.JobCodeDescription ?? string.Empty,
                    Role: matchedUser?.Role ?? "faculty");
            })
            .Where(user => !string.IsNullOrWhiteSpace(user.IamId))
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

    private static string GetDesignation(string role, Server.Core.Domain.Person? person)
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

public sealed record AdminFacultyResponse(
    IReadOnlyList<AdminDepartmentResponse> Departments,
    IReadOnlyList<AdminUserResponse> FacultyUsers);
