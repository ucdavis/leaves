using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

namespace Server.Services;

public interface IApprovalWorkspaceService
{
    Task<ApprovalWorkspaceResponse?> GetWorkspaceAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ApprovalDecisionResult> SubmitDecisionAsync(
        ClaimsPrincipal principal,
        int requestId,
        SubmitApprovalDecisionRequest request,
        CancellationToken cancellationToken);

    Task<bool> DecideRequestAsync(
        ClaimsPrincipal principal,
        int requestId,
        LeaveRequestActionType decision,
        CancellationToken cancellationToken);
}

public sealed class ApprovalWorkspaceService : IApprovalWorkspaceService
{
    private const string CaoRole = "CAO";

    private readonly IAdminDirectoryDataService _directoryDataService;
    private readonly AppDbContext _db;

    public ApprovalWorkspaceService(
        IAdminDirectoryDataService directoryDataService,
        AppDbContext db)
    {
        _directoryDataService = directoryDataService;
        _db = db;
    }

    public async Task<ApprovalWorkspaceResponse?> GetWorkspaceAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var context = await ResolveWorkspaceContextAsync(principal, cancellationToken);
        if (context == null)
        {
            return null;
        }

        var scopedLeaveRequests = await LoadScopedLeaveRequestsAsync(
            context.FacultyIds,
            cancellationToken);

        var leaveTypeIds = scopedLeaveRequests
            .SelectMany(request => new[] { request.LeaveTypeId, request.PayLeaveTypeId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var leaveTypesById = await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => leaveTypeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, cancellationToken);

        var leaves = scopedLeaveRequests
            .Where(request =>
                request.Status == LeaveRequestStatus.Approved ||
                request.Status == LeaveRequestStatus.PendingApproval)
            .Select(request => new ApprovalWorkspaceLeaveResponse(
                EndDate: request.EndDate.ToString("yyyy-MM-dd"),
                FacultyId: request.IamId.Trim(),
                Id: request.Id,
                LeaveType: GetLeaveTypeName(request, leaveTypesById),
                StartDate: request.StartDate.ToString("yyyy-MM-dd"),
                Status: request.Status.ToString()))
            .ToList();

        var pendingRequests = scopedLeaveRequests
            .Where(request => request.Status == LeaveRequestStatus.PendingApproval)
            .Select(request => new ApprovalWorkspaceRequestResponse(
                DepartmentName: request.ReportingDepartmentNameSnapshot,
                EndDate: request.EndDate.ToString("yyyy-MM-dd"),
                FacultyInitials: BuildInitials(GetFacultyDisplayName(
                    request.IamId,
                    context.DirectoryData.CurrentEmployees,
                    context.DirectoryData.AppUsers)),
                FacultyName: GetFacultyDisplayName(
                    request.IamId,
                    context.DirectoryData.CurrentEmployees,
                    context.DirectoryData.AppUsers),
                Id: request.Id,
                LeaveType: GetLeaveTypeName(request, leaveTypesById),
                Note: request.Note,
                StartDate: request.StartDate.ToString("yyyy-MM-dd"),
                TotalHours: request.TotalHours))
            .ToList();

        return new ApprovalWorkspaceResponse(
            Scope: context.Scope,
            Faculty: context.Faculty,
            Leaves: leaves,
            PendingRequests: pendingRequests);
    }

    private async Task<ApprovalWorkspaceContext?> ResolveWorkspaceContextAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var appUser = await ResolveAppUserAsync(principal, cancellationToken);
        if (appUser == null)
        {
            return null;
        }

        var directoryData = await _directoryDataService.LoadDirectoryDataAsync(cancellationToken);
        var currentEmployee = directoryData.CurrentEmployees.FirstOrDefault(employee =>
            NormalizeKey(employee.IamId) == NormalizeKey(appUser.IamId));
        if (currentEmployee == null)
        {
            return null;
        }

        var currentDepartmentCode = NormalizeKey(currentEmployee.ResolvedReportingDepartmentCode);
        if (string.IsNullOrWhiteSpace(currentDepartmentCode))
        {
            return null;
        }

        var departmentByCode = directoryData.Departments.ToDictionary(
            department => NormalizeKey(department.DepartmentCode),
            department => department,
            StringComparer.OrdinalIgnoreCase);

        if (!departmentByCode.TryGetValue(currentDepartmentCode, out var currentDepartment))
        {
            return null;
        }

        var normalizedIamId = NormalizeKey(appUser.IamId);
        var clusterId = currentDepartment.ClusterId;

        var hasCurrentCaoAssignment = clusterId.HasValue &&
            directoryData.CurrentCaoAssignmentsByCluster.TryGetValue(clusterId.Value, out var currentCaoAssignment) &&
            NormalizeKey(currentCaoAssignment.IamId) == normalizedIamId;
        var hasCurrentChairAssignment =
            directoryData.CurrentChairAssignmentsByDepartment.TryGetValue(currentDepartmentCode, out var currentChairAssignment) &&
            NormalizeKey(currentChairAssignment.IamId) == normalizedIamId;

        string scope;
        if (HasRole(principal, CaoRole) && hasCurrentCaoAssignment)
        {
            scope = "cluster";
        }
        else if (hasCurrentChairAssignment)
        {
            scope = "team";
        }
        else
        {
            return null;
        }

        var faculty = BuildFacultyRoster(
            directoryData.CurrentEmployees,
            departmentByCode,
            scope,
            currentDepartmentCode,
            clusterId);

        return new ApprovalWorkspaceContext(
            AppUser: appUser,
            DirectoryData: directoryData,
            Faculty: faculty,
            FacultyIds: faculty.Select(item => item.Id).ToArray(),
            Scope: scope);
    }

    public async Task<ApprovalDecisionResult> SubmitDecisionAsync(
        ClaimsPrincipal principal,
        int requestId,
        SubmitApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var status = NormalizeKey(request.Decision) switch
        {
            "approved" => LeaveRequestStatus.Approved,
            "denied" => LeaveRequestStatus.Denied,
            _ => (LeaveRequestStatus?)null,
        };

        if (!status.HasValue)
        {
            return ApprovalDecisionResult.Invalid("Decision must be either approved or denied.");
        }

        var appUser = await ResolveAppUserAsync(principal, cancellationToken);
        if (appUser == null)
        {
            return ApprovalDecisionResult.NotFound();
        }

        var scopedFacultyIds = await LoadScopedFacultyIdsAsync(principal, appUser, cancellationToken);
        if (scopedFacultyIds == null)
        {
            return ApprovalDecisionResult.NotFound();
        }

        var leaveRequest = await _db.LeaveRequests
            .SingleOrDefaultAsync(item =>
                item.Id == requestId &&
                item.Status == LeaveRequestStatus.PendingApproval &&
                scopedFacultyIds.Contains(item.IamId.Trim()),
                cancellationToken);

        if (leaveRequest == null)
        {
            return ApprovalDecisionResult.NotFound();
        }

        var nowUtc = DateTime.UtcNow;
        leaveRequest.Status = status.Value;
        leaveRequest.UpdatedUtc = nowUtc;
        _db.LeaveRequestActions.Add(new LeaveRequestAction
        {
            LeaveRequestId = leaveRequest.Id,
            ActionType = status.Value == LeaveRequestStatus.Approved
                ? LeaveRequestActionType.Approved
                : LeaveRequestActionType.Denied,
            ActorAppUserId = appUser.Id,
            ActorIamId = appUser.IamId,
            ActionAt = nowUtc,
            Comment = request.Comment,
            IsSelfAction = false,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return ApprovalDecisionResult.Success();
    }

    public async Task<bool> DecideRequestAsync(
        ClaimsPrincipal principal,
        int requestId,
        LeaveRequestActionType decision,
        CancellationToken cancellationToken)
    {
        var decisionText = decision == LeaveRequestActionType.Approved
            ? "approved"
            : "denied";
        var result = await SubmitDecisionAsync(
            principal,
            requestId,
            new SubmitApprovalDecisionRequest(decisionText, null),
            cancellationToken);

        return result.Succeeded;
    }

    private static IReadOnlyList<ApprovalWorkspaceFacultyResponse> BuildFacultyRoster(
        IReadOnlyList<CurrentEmployee> currentEmployees,
        IReadOnlyDictionary<string, Department> departmentByCode,
        string scope,
        string currentDepartmentCode,
        int? currentClusterId)
    {
        var faculty = currentEmployees
            .Where(employee => employee.HasCurrentAccrualRecord)
            .Where(employee =>
            {
                var departmentCode = NormalizeKey(employee.ResolvedReportingDepartmentCode);
                if (string.IsNullOrWhiteSpace(departmentCode))
                {
                    return false;
                }

                if (!departmentByCode.TryGetValue(departmentCode, out var department))
                {
                    return false;
                }

                return scope == "cluster"
                    ? department.ClusterId == currentClusterId
                    : departmentCode == currentDepartmentCode;
            })
            .Select(employee =>
            {
                var departmentCode = NormalizeKey(employee.ResolvedReportingDepartmentCode);
                departmentByCode.TryGetValue(departmentCode, out var department);

                return new ApprovalWorkspaceFacultyResponse(
                    DepartmentName: department?.DepartmentName ?? employee.ResolvedReportingDepartmentName ?? "Unknown",
                    Id: employee.IamId.Trim(),
                    Name: employee.DisplayName?.Trim() ?? employee.IamId.Trim());
            })
            .OrderBy(facultyMember => facultyMember.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(facultyMember => facultyMember.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return faculty;
    }

    private async Task<List<LeaveRequest>> LoadScopedLeaveRequestsAsync(
        IReadOnlyCollection<string> scopedFacultyIds,
        CancellationToken cancellationToken)
    {
        var facultyIds = scopedFacultyIds.ToArray();
        var query = _db.LeaveRequests
            .AsNoTracking()
            .Where(request =>
                request.Status == LeaveRequestStatus.PendingApproval ||
                request.Status == LeaveRequestStatus.Approved)
            .Where(request => facultyIds.Contains(request.IamId));

        return await query
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlySet<string>?> LoadScopedFacultyIdsAsync(
        ClaimsPrincipal principal,
        AppUser appUser,
        CancellationToken cancellationToken)
    {
        var directoryData = await _directoryDataService.LoadDirectoryDataAsync(cancellationToken);
        var currentEmployee = directoryData.CurrentEmployees.FirstOrDefault(employee =>
            NormalizeKey(employee.IamId) == NormalizeKey(appUser.IamId));
        if (currentEmployee == null)
        {
            return null;
        }

        var currentDepartmentCode = NormalizeKey(currentEmployee.ResolvedReportingDepartmentCode);
        if (string.IsNullOrWhiteSpace(currentDepartmentCode))
        {
            return null;
        }

        var departmentByCode = directoryData.Departments.ToDictionary(
            department => NormalizeKey(department.DepartmentCode),
            department => department,
            StringComparer.OrdinalIgnoreCase);

        if (!departmentByCode.TryGetValue(currentDepartmentCode, out var currentDepartment))
        {
            return null;
        }

        var normalizedIamId = NormalizeKey(appUser.IamId);
        var clusterId = currentDepartment.ClusterId;
        var hasCurrentCaoAssignment = clusterId.HasValue &&
            directoryData.CurrentCaoAssignmentsByCluster.TryGetValue(clusterId.Value, out var currentCaoAssignment) &&
            NormalizeKey(currentCaoAssignment.IamId) == normalizedIamId;
        var hasCurrentChairAssignment =
            directoryData.CurrentChairAssignmentsByDepartment.TryGetValue(currentDepartmentCode, out var currentChairAssignment) &&
            NormalizeKey(currentChairAssignment.IamId) == normalizedIamId;

        string scope;
        if (HasRole(principal, CaoRole) && hasCurrentCaoAssignment)
        {
            scope = "cluster";
        }
        else if (hasCurrentChairAssignment)
        {
            scope = "team";
        }
        else
        {
            return null;
        }

        return BuildFacultyRoster(
                directoryData.CurrentEmployees,
                departmentByCode,
                scope,
                currentDepartmentCode,
                clusterId)
            .Select(faculty => faculty.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetFacultyDisplayName(
        string iamId,
        IReadOnlyList<CurrentEmployee> currentEmployees,
        IReadOnlyList<AppUser> appUsers)
    {
        var normalizedIamId = NormalizeKey(iamId);

        var currentEmployee = currentEmployees.FirstOrDefault(employee =>
            NormalizeKey(employee.IamId) == normalizedIamId);
        if (!string.IsNullOrWhiteSpace(currentEmployee?.DisplayName))
        {
            return currentEmployee.DisplayName.Trim();
        }

        var appUser = appUsers.FirstOrDefault(user => NormalizeKey(user.IamId) == normalizedIamId);
        if (!string.IsNullOrWhiteSpace(appUser?.DisplayName))
        {
            return appUser.DisplayName.Trim();
        }

        return iamId.Trim();
    }

    private static string GetLeaveTypeName(
        LeaveRequest request,
        IReadOnlyDictionary<int, LeaveType> leaveTypesById)
    {
        return leaveTypesById.TryGetValue(request.LeaveTypeId, out var leaveType)
            ? leaveType.DisplayName
            : "Vacation";
    }

    private async Task<AppUser?> ResolveAppUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return null;
        }

        if (Guid.TryParse(userId, out var entraObjectId))
        {
            var appUserByObjectId = await _db.AppUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);

            if (appUserByObjectId != null)
            {
                return appUserByObjectId;
            }
        }

        var normalizedUserId = NormalizeKey(userId);
        return await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.IamId.Trim().ToLower() == normalizedUserId,
                cancellationToken);
    }

    private static string BuildInitials(string name)
    {
        var initials = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part[0])
            .Take(2)
            .ToArray();

        return initials.Length > 0
            ? new string(initials).ToUpperInvariant()
            : "?";
    }

    private static bool HasRole(ClaimsPrincipal principal, string roleName)
    {
        return principal.Claims.Any(claim =>
            claim.Type == ClaimTypes.Role &&
            claim.Value.Equals(roleName, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeKey(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private sealed record ApprovalWorkspaceContext(
        AppUser AppUser,
        AdminDirectoryData DirectoryData,
        IReadOnlyList<ApprovalWorkspaceFacultyResponse> Faculty,
        IReadOnlyCollection<string> FacultyIds,
        string Scope);
}

public sealed record ApprovalWorkspaceResponse(
    string Scope,
    IReadOnlyList<ApprovalWorkspaceFacultyResponse> Faculty,
    IReadOnlyList<ApprovalWorkspaceLeaveResponse> Leaves,
    IReadOnlyList<ApprovalWorkspaceRequestResponse> PendingRequests);

public sealed record ApprovalWorkspaceFacultyResponse(
    string DepartmentName,
    string Id,
    string Name);

public sealed record ApprovalWorkspaceLeaveResponse(
    string EndDate,
    string FacultyId,
    int Id,
    string LeaveType,
    string StartDate,
    string Status);

public sealed record ApprovalWorkspaceRequestResponse(
    string DepartmentName,
    string EndDate,
    string FacultyInitials,
    string FacultyName,
    int Id,
    string LeaveType,
    string? Note,
    string StartDate,
    decimal TotalHours);

public sealed record SubmitApprovalDecisionRequest(
    string Decision,
    string? Comment);

public sealed record ApprovalDecisionResult(
    bool Succeeded,
    bool MissingRequest,
    Dictionary<string, string[]> Errors)
{
    public static ApprovalDecisionResult Success() =>
        new(true, false, []);

    public static ApprovalDecisionResult NotFound() =>
        new(false, true, []);

    public static ApprovalDecisionResult Invalid(string message) =>
        new(false, false, new Dictionary<string, string[]> { ["decision"] = [message] });
}
