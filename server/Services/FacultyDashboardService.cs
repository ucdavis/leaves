using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

namespace Server.Services;

public interface IFacultyDashboardService
{
    Task<FacultyDashboardResponse?> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<CreateLeaveRequestResult> CreateLeaveRequestAsync(
        ClaimsPrincipal principal,
        CreateFacultyLeaveRequest request,
        CancellationToken cancellationToken);
}

public sealed class FacultyDashboardService : IFacultyDashboardService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FacultyDashboardService> _logger;

    public FacultyDashboardService(AppDbContext db, ILogger<FacultyDashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FacultyDashboardResponse?> GetDashboardAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var appUser = await ResolveAppUserAsync(principal, cancellationToken);
        if (appUser == null)
        {
            return null;
        }

        var iamId = NormalizeIamId(appUser.IamId);
        var employee = await GetCurrentEmployeeAsync(iamId, cancellationToken);
        var accrualBalances = await GetCurrentAccrualBalancesAsync(iamId, cancellationToken);
        var recentRequests = await GetRecentLeaveRequestsAsync(appUser.Id, cancellationToken);
        var leaveTypes = await GetLeaveTypesAsync(cancellationToken);

        var balanceSummary = BuildBalanceSummary(accrualBalances);
        var pendingStatus = LeaveRequestStatus.PendingApproval.ToString();
        var approvedStatus = LeaveRequestStatus.Approved.ToString();
        var pendingCount = recentRequests.Count(request => request.Status == pendingStatus);
        var approvedCount = recentRequests.Count(request => request.Status == approvedStatus);

        return new FacultyDashboardResponse(
            Faculty: new FacultyProfileResponse(
                IamId: iamId,
                EmployeeId: employee?.EmployeeId?.Trim() ?? appUser.EmployeeId?.Trim(),
                Name: employee?.DisplayName ?? appUser.DisplayName ?? iamId,
                Email: employee?.Email ?? appUser.Email,
                DepartmentCode: employee?.ResolvedReportingDepartmentCode,
                DepartmentName: employee?.ResolvedReportingDepartmentName,
                EmployeeClass: employee?.EmployeeClassDescription,
                JobTitle: employee?.JobCodeDescription,
                LatestSnapshotDate: employee?.LatestAsOfDate),
            Snapshot: new FacultyDashboardSnapshotResponse(
                PendingRequests: pendingCount,
                ApprovedRequests: approvedCount,
                AvailableBalanceHours: balanceSummary.AvailableBalanceHours,
                AccrualsApproachingCap: balanceSummary.AccrualsApproachingCap),
            AccrualBalances: accrualBalances
                .Select(balance => new FacultyAccrualBalanceResponse(
                    TypeLabel: balance.TypeLabel,
                    CalculatedBalance: balance.CalculatedBal,
                    AccrualLimit: balance.AccrualLimit,
                    AccrualPercentage: balance.AccrualPercentage,
                    ApproachingMax: balance.ApproachingMax,
                    LatestAsOfDate: balance.LatestAsOfDate,
                    HasDivergentPositionBalances: balance.HasDivergentPositionBalances))
                .ToList(),
            RecentRequests: recentRequests,
            LeaveTypes: leaveTypes
                .Select(type => new FacultyLeaveTypeResponse(
                    Id: type.Id,
                    DisplayName: type.DisplayName,
                    HasAccrualBalance: type.HasAccrualBalance))
                .ToList());
    }

    public async Task<CreateLeaveRequestResult> CreateLeaveRequestAsync(
        ClaimsPrincipal principal,
        CreateFacultyLeaveRequest request,
        CancellationToken cancellationToken)
    {
        var appUser = await ResolveAppUserAsync(principal, cancellationToken);
        if (appUser == null)
        {
            return CreateLeaveRequestResult.UserNotFound();
        }

        var validationErrors = ValidateRequestShape(request);
        if (validationErrors.Count > 0)
        {
            return CreateLeaveRequestResult.Invalid(validationErrors);
        }

        var leaveType = await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => type.Id == request.LeaveTypeId && type.IsActive)
            .SingleOrDefaultAsync(cancellationToken);

        if (leaveType == null)
        {
            return CreateLeaveRequestResult.Invalid("leaveTypeId", "Select an active leave type.");
        }

        LeaveType? payLeaveType = null;
        if (request.PayLeaveTypeId.HasValue)
        {
            payLeaveType = await _db.LeaveTypes
                .AsNoTracking()
                .Where(type => type.Id == request.PayLeaveTypeId.Value && type.IsActive)
                .SingleOrDefaultAsync(cancellationToken);

            if (payLeaveType == null)
            {
                return CreateLeaveRequestResult.Invalid("payLeaveTypeId", "Select an active pay leave type.");
            }
        }

        var iamId = NormalizeIamId(appUser.IamId);
        var employee = await GetCurrentEmployeeAsync(iamId, cancellationToken);
        var department = await ResolveReportingDepartmentAsync(employee, cancellationToken);
        if (department == null)
        {
            return CreateLeaveRequestResult.Invalid(
                "department",
                "A reporting department is required before a leave request can be submitted.");
        }

        var submittedAt = DateTime.UtcNow;
        var leaveRequest = new LeaveRequest
        {
            AppUserId = appUser.Id,
            ClusterIdSnapshot = department.ClusterId,
            CoveragePlan = request.CoveragePlan?.Trim(),
            EmployeeId = employee?.EmployeeId?.Trim() ?? appUser.EmployeeId?.Trim(),
            EndDate = request.EndDate,
            IamId = iamId,
            LeaveTypeId = leaveType.Id,
            Note = request.Note?.Trim(),
            PayLeaveTypeId = payLeaveType?.Id,
            ReportingDepartmentCodeSnapshot = department.DepartmentCode,
            ReportingDepartmentNameSnapshot = department.DepartmentName,
            StartDate = request.StartDate,
            Status = department.WorkflowMode == WorkflowMode.ApprovalRequired
                ? LeaveRequestStatus.PendingApproval
                : LeaveRequestStatus.Approved,
            SubmittedAt = submittedAt,
            TotalHours = request.TotalHours,
            WorkflowModeSnapshot = department.WorkflowMode,
            CreatedUtc = submittedAt,
            UpdatedUtc = submittedAt,
        };

        _db.LeaveRequests.Add(leaveRequest);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Faculty leave request {LeaveRequestId} submitted for IAM {IamId}.",
            leaveRequest.Id,
            iamId);

        return CreateLeaveRequestResult.Created(leaveRequest.Id);
    }

    private async Task<AppUser?> ResolveAppUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return null;
        }

        if (Guid.TryParse(userId, out var entraObjectId))
        {
            var userByObjectId = await _db.AppUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);

            if (userByObjectId != null)
            {
                return userByObjectId;
            }
        }

        var iamId = NormalizeIamId(userId);
        return await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.IamId == iamId, cancellationToken);
    }

    private async Task<CurrentEmployee?> GetCurrentEmployeeAsync(string iamId, CancellationToken cancellationToken)
    {
        return await _db.CurrentEmployees
            .AsNoTracking()
            .Where(employee => employee.IamId == iamId)
            .Select(employee => new CurrentEmployee
            {
                DisplayName = employee.DisplayName,
                Email = employee.Email,
                EmployeeClassDescription = employee.EmployeeClassDescription,
                EmployeeId = employee.EmployeeId,
                HasCurrentAccrualRecord = employee.HasCurrentAccrualRecord,
                HasReportingDepartmentOverride = employee.HasReportingDepartmentOverride,
                HrStatus = employee.HrStatus,
                IamId = employee.IamId,
                JobCodeDescription = employee.JobCodeDescription,
                LatestAsOfDate = employee.LatestAsOfDate,
                ReportingDepartmentOverrideId = employee.ReportingDepartmentOverrideId,
                ResolvedReportingDepartmentCode = employee.ResolvedReportingDepartmentCode,
                ResolvedReportingDepartmentName = employee.ResolvedReportingDepartmentName,
                SourceDepartmentCode = employee.SourceDepartmentCode,
                SourceDepartmentName = employee.SourceDepartmentName,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<CurrentAccrualBalance>> GetCurrentAccrualBalancesAsync(
        string iamId,
        CancellationToken cancellationToken)
    {
        return await _db.CurrentAccrualBalances
            .AsNoTracking()
            .Where(balance => balance.IamId == iamId)
            .OrderBy(balance => balance.TypeLabel)
            .Select(balance => new CurrentAccrualBalance
            {
                AccrualLimit = balance.AccrualLimit,
                AccrualPercentage = balance.AccrualPercentage,
                ApproachingMax = balance.ApproachingMax,
                CalculatedBal = balance.CalculatedBal,
                EmployeeId = balance.EmployeeId,
                HasDivergentPositionBalances = balance.HasDivergentPositionBalances,
                IamId = balance.IamId,
                LatestAsOfDate = balance.LatestAsOfDate,
                LeaveTypeNumber = balance.LeaveTypeNumber,
                MaxCalculatedBal = balance.MaxCalculatedBal,
                MinCalculatedBal = balance.MinCalculatedBal,
                PositionRowCount = balance.PositionRowCount,
                TypeLabel = balance.TypeLabel,
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<FacultyLeaveRequestResponse>> GetRecentLeaveRequestsAsync(
        int appUserId,
        CancellationToken cancellationToken)
    {
        var requests = await _db.LeaveRequests
            .AsNoTracking()
            .Where(request => request.AppUserId == appUserId)
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id)
            .Take(24)
            .ToListAsync(cancellationToken);

        var leaveTypeIds = requests
            .SelectMany(request => new[] { request.LeaveTypeId, request.PayLeaveTypeId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var leaveTypesById = await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => leaveTypeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, cancellationToken);

        return requests
            .Select(request =>
            {
                var leaveTypeName = leaveTypesById.TryGetValue(request.LeaveTypeId, out var leaveType)
                    ? leaveType.DisplayName
                    : "Unknown";
                var payLeaveTypeName = request.PayLeaveTypeId.HasValue &&
                    leaveTypesById.TryGetValue(request.PayLeaveTypeId.Value, out var payLeaveType)
                        ? payLeaveType.DisplayName
                        : null;

                return new FacultyLeaveRequestResponse(
                    Id: request.Id,
                    LeaveType: leaveTypeName,
                    PayLeaveType: payLeaveTypeName,
                    Status: request.Status.ToString(),
                    StartDate: request.StartDate,
                    EndDate: request.EndDate,
                    TotalHours: request.TotalHours,
                    SubmittedAt: request.SubmittedAt,
                    WorkflowMode: request.WorkflowModeSnapshot.ToString(),
                    DepartmentName: request.ReportingDepartmentNameSnapshot);
            })
            .ToList();
    }

    private async Task<List<LeaveType>> GetLeaveTypesAsync(CancellationToken cancellationToken)
    {
        return await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => type.IsActive)
            .OrderBy(type => type.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private async Task<Department?> ResolveReportingDepartmentAsync(
        CurrentEmployee? employee,
        CancellationToken cancellationToken)
    {
        var departmentCode = employee?.ResolvedReportingDepartmentCode?.Trim();
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return null;
        }

        return await _db.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(department => department.DepartmentCode == departmentCode, cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateRequestShape(CreateFacultyLeaveRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request.LeaveTypeId <= 0)
        {
            errors["leaveTypeId"] = ["Select a leave type."];
        }

        if (request.EndDate < request.StartDate)
        {
            errors["endDate"] = ["End date must be on or after the start date."];
        }

        if (request.TotalHours <= 0)
        {
            errors["totalHours"] = ["Total hours must be greater than zero."];
        }

        if (request.TotalHours > 240)
        {
            errors["totalHours"] = ["Total hours must be 240 or fewer for one request."];
        }

        return errors;
    }

    private static FacultyBalanceSummary BuildBalanceSummary(IReadOnlyCollection<CurrentAccrualBalance> balances)
    {
        var availableBalanceHours = balances
            .Where(balance => balance.CalculatedBal > 0)
            .Sum(balance => balance.CalculatedBal);

        var accrualsApproachingCap = balances.Count(balance =>
            string.Equals(balance.ApproachingMax, "Y", StringComparison.OrdinalIgnoreCase) ||
            balance.AccrualPercentage >= 80);

        return new FacultyBalanceSummary(availableBalanceHours, accrualsApproachingCap);
    }

    private static string NormalizeIamId(string value) => value.Trim();

    private sealed record FacultyBalanceSummary(decimal AvailableBalanceHours, int AccrualsApproachingCap);
}

public sealed record FacultyDashboardResponse(
    FacultyProfileResponse Faculty,
    FacultyDashboardSnapshotResponse Snapshot,
    IReadOnlyCollection<FacultyAccrualBalanceResponse> AccrualBalances,
    IReadOnlyCollection<FacultyLeaveRequestResponse> RecentRequests,
    IReadOnlyCollection<FacultyLeaveTypeResponse> LeaveTypes);

public sealed record FacultyProfileResponse(
    string IamId,
    string? EmployeeId,
    string Name,
    string? Email,
    string? DepartmentCode,
    string? DepartmentName,
    string? EmployeeClass,
    string? JobTitle,
    DateOnly? LatestSnapshotDate);

public sealed record FacultyDashboardSnapshotResponse(
    int PendingRequests,
    int ApprovedRequests,
    decimal AvailableBalanceHours,
    int AccrualsApproachingCap);

public sealed record FacultyAccrualBalanceResponse(
    string TypeLabel,
    decimal CalculatedBalance,
    decimal AccrualLimit,
    decimal AccrualPercentage,
    string ApproachingMax,
    DateOnly LatestAsOfDate,
    bool HasDivergentPositionBalances);

public sealed record FacultyLeaveRequestResponse(
    int Id,
    string LeaveType,
    string? PayLeaveType,
    string Status,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalHours,
    DateTime SubmittedAt,
    string WorkflowMode,
    string DepartmentName);

public sealed record FacultyLeaveTypeResponse(
    int Id,
    string DisplayName,
    bool HasAccrualBalance);

public sealed record CreateFacultyLeaveRequest(
    int LeaveTypeId,
    int? PayLeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalHours,
    string? Note,
    string? CoveragePlan);

public sealed record CreateLeaveRequestResult(
    bool Succeeded,
    bool MissingUser,
    int? LeaveRequestId,
    Dictionary<string, string[]> Errors)
{
    public static CreateLeaveRequestResult Created(int leaveRequestId) =>
        new(true, false, leaveRequestId, []);

    public static CreateLeaveRequestResult Invalid(string key, string message) =>
        new(false, false, null, new Dictionary<string, string[]> { [key] = [message] });

    public static CreateLeaveRequestResult Invalid(Dictionary<string, string[]> errors) =>
        new(false, false, null, errors);

    public static CreateLeaveRequestResult UserNotFound() =>
        new(false, true, null, []);
}
