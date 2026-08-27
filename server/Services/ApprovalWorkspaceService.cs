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

        var isCao = HasRole(principal, CaoRole);
        var scope = isCao ? "cluster" : "team";
        var clusterId = currentDepartment.ClusterId;
        if (scope == "cluster" && !clusterId.HasValue)
        {
            return null;
        }

        var faculty = BuildFacultyRoster(
            directoryData.CurrentEmployees,
            departmentByCode,
            scope,
            currentDepartmentCode,
            clusterId);
        var facultyIds = faculty.Select(item => item.Id).ToArray();

        var scopedLeaveRequests = await LoadScopedLeaveRequestsAsync(
            facultyIds,
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
                    directoryData.CurrentEmployees,
                    directoryData.AppUsers)),
                FacultyName: GetFacultyDisplayName(
                    request.IamId,
                    directoryData.CurrentEmployees,
                    directoryData.AppUsers),
                Id: request.Id,
                LeaveType: GetLeaveTypeName(request, leaveTypesById),
                Note: request.Note,
                StartDate: request.StartDate.ToString("yyyy-MM-dd"),
                TotalHours: request.TotalHours))
            .ToList();

        return new ApprovalWorkspaceResponse(
            Scope: scope,
            Faculty: faculty,
            Leaves: leaves,
            PendingRequests: pendingRequests);
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
                user => NormalizeKey(user.IamId) == normalizedUserId,
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
