using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

namespace Server.Services;

public interface IFacultyDashboardService
{
    Task<FacultyDashboardResponse?> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FacultyDashboardViewerResult> GetDashboardForViewerAsync(
        ClaimsPrincipal principal,
        string iamId,
        CancellationToken cancellationToken);
    Task<FacultyDashboardResponse?> GetHistoryAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FacultyLeaveRequestResponse?> GetRequestAsync(
        ClaimsPrincipal principal,
        int leaveRequestId,
        CancellationToken cancellationToken);

    Task<CreateLeaveRequestResult> CreateLeaveRequestAsync(
        ClaimsPrincipal principal,
        CreateFacultyLeaveRequest request,
        CancellationToken cancellationToken);
}

public sealed class FacultyDashboardService : IFacultyDashboardService
{
    private const string CaoRole = "CAO";
    private const string ChairRole = "Chair";
    private const int RecentRequestsLimit = 24;
    private const string FamilyCareLeaveTypeKey = "FamilyCare";
    private const string ProfessionalDevelopmentLeaveTypeLabel = "Professional Development";
    private const string SabbaticalLeaveTypeLabel = "Sabbatical";
    private const string FmlaLeaveTypeLabel = "FMLA";
    private const string ProfessionalDevelopmentLeaveTypeKey = "ProfessionalDevelopment";
    private const string SabbaticalLeaveTypeKey = "Sabbatical";

    private static readonly string[] DesiredLeaveTypeLabels =
    [
        "Vacation",
        "Sick Leave",
        "Professional Development",
        "Sabbatical",
        FmlaLeaveTypeLabel,
    ];

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
        var department = await ResolveReportingDepartmentAsync(employee, cancellationToken);
        var accrualBalances = await GetCurrentAccrualBalancesAsync(iamId, cancellationToken);
        var (pendingCount, approvedCount) = await GetRequestSnapshotCountsAsync(iamId, cancellationToken);
        var recentRequests = await GetLeaveRequestsAsync(
            appUser.Id,
            RecentRequestsLimit,
            cancellationToken);
        var leaveTypes = await GetLeaveTypesAsync(cancellationToken);

        return BuildDashboardResponse(
            appUser,
            iamId,
            employee,
            department,
            accrualBalances,
            recentRequests,
            leaveTypes,
            pendingCount,
            approvedCount);
    }

    public async Task<FacultyDashboardViewerResult> GetDashboardForViewerAsync(
        ClaimsPrincipal principal,
        string iamId,
        CancellationToken cancellationToken)
    {
        var viewer = await ResolveAppUserAsync(principal, cancellationToken);
        if (viewer == null)
        {
            return FacultyDashboardViewerResult.ViewerNotFound();
        }

        var normalizedTargetIamId = NormalizeIamId(iamId);
        if (string.Equals(viewer.IamId, normalizedTargetIamId, StringComparison.OrdinalIgnoreCase))
        {
            var ownDashboard = await GetDashboardAsync(principal, cancellationToken);
            return ownDashboard == null
                ? FacultyDashboardViewerResult.TargetNotFound()
                : FacultyDashboardViewerResult.Success(ownDashboard);
        }

        var targetUser = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.IamId == normalizedTargetIamId,
                cancellationToken);
        var targetEmployee = await GetCurrentEmployeeAsync(
            normalizedTargetIamId,
            cancellationToken);

        if (targetEmployee == null)
        {
            return FacultyDashboardViewerResult.TargetNotFound();
        }

        var targetDepartment = await ResolveReportingDepartmentAsync(
            targetEmployee,
            cancellationToken);

        if (targetDepartment == null)
        {
            return FacultyDashboardViewerResult.Forbidden();
        }

        var assignedCaoClusterIds = await GetActiveCaoClusterIdsAsync(
            viewer.IamId,
            cancellationToken);
        var isCao = HasRole(principal, CaoRole);
        var canViewTarget = isCao
            ? targetDepartment.ClusterId.HasValue &&
                assignedCaoClusterIds.Contains(targetDepartment.ClusterId.Value)
            : HasRole(principal, ChairRole) &&
                (await GetActiveChairDepartmentCodesAsync(viewer.IamId, cancellationToken))
                    .Contains(targetDepartment.DepartmentCode);

        if (!canViewTarget)
        {
            return FacultyDashboardViewerResult.Forbidden();
        }

        var accrualBalances = await GetCurrentAccrualBalancesAsync(
            normalizedTargetIamId,
            cancellationToken);
        var (pendingCount, approvedCount) = await GetRequestSnapshotCountsAsync(
            normalizedTargetIamId,
            cancellationToken);
        var recentRequests = targetUser == null
            ? []
            : await GetLeaveRequestsAsync(
                targetUser.Id,
                RecentRequestsLimit,
                cancellationToken);
        var leaveTypes = await GetLeaveTypesAsync(cancellationToken);

        return FacultyDashboardViewerResult.Success(
            BuildDashboardResponse(
                targetUser ?? new AppUser
                {
                    DisplayName = targetEmployee.DisplayName,
                    EntraObjectId = Guid.Empty,
                    FirstLoginUtc = DateTime.MinValue,
                    IamId = normalizedTargetIamId,
                    IsActive = true,
                },
                normalizedTargetIamId,
                targetEmployee,
                targetDepartment,
                accrualBalances,
                recentRequests,
                leaveTypes,
                pendingCount,
                approvedCount));
    }

    public async Task<FacultyDashboardResponse?> GetHistoryAsync(
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
        var department = await ResolveReportingDepartmentAsync(employee, cancellationToken);
        var accrualBalances = await GetCurrentAccrualBalancesAsync(iamId, cancellationToken);
        var (pendingCount, approvedCount) = await GetRequestSnapshotCountsAsync(iamId, cancellationToken);
        var allRequests = await GetLeaveRequestsAsync(appUser.Id, null, cancellationToken);
        var leaveTypes = await GetLeaveTypesAsync(cancellationToken);

        return BuildDashboardResponse(
            appUser,
            iamId,
            employee,
            department,
            accrualBalances,
            allRequests,
            leaveTypes,
            pendingCount,
            approvedCount);
    }

    public async Task<FacultyLeaveRequestResponse?> GetRequestAsync(
        ClaimsPrincipal principal,
        int leaveRequestId,
        CancellationToken cancellationToken)
    {
        var appUser = await ResolveAppUserAsync(principal, cancellationToken);
        if (appUser == null)
        {
            return null;
        }

        var request = await _db.LeaveRequests
            .AsNoTracking()
            .Where(leaveRequest => leaveRequest.AppUserId == appUser.Id && leaveRequest.Id == leaveRequestId)
            .SingleOrDefaultAsync(cancellationToken);

        if (request == null)
        {
            return null;
        }

        return await BuildFacultyLeaveRequestResponseAsync(request, cancellationToken);
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

        var leaveType = await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => type.Id == request.LeaveTypeId && type.IsActive)
            .SingleOrDefaultAsync(cancellationToken);

        if (leaveType == null)
        {
            return CreateLeaveRequestResult.Invalid("leaveTypeId", "Select an active leave type.");
        }

        var validationErrors = ValidateRequestShape(request, leaveType);
        if (validationErrors.Count > 0)
        {
            return CreateLeaveRequestResult.Invalid(validationErrors);
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
        var hasActiveOverlap = await HasActiveOverlappingLeaveRequestAsync(
            iamId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        if (hasActiveOverlap)
        {
            return CreateLeaveRequestResult.Invalid(
                "startDate",
                "You already have a leave request that includes one or more of these dates.");
        }

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

    internal async Task<bool> HasActiveOverlappingLeaveRequestAsync(
        string iamId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _db.LeaveRequests
            .AsNoTracking()
            .AnyAsync(
                existing =>
                    existing.IamId == iamId &&
                    (existing.Status == LeaveRequestStatus.PendingApproval ||
                        existing.Status == LeaveRequestStatus.Approved) &&
                    existing.StartDate <= endDate &&
                    existing.EndDate >= startDate,
                cancellationToken);
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

    private static FacultyDashboardResponse BuildDashboardResponse(
        AppUser appUser,
        string iamId,
        CurrentEmployee? employee,
        Department? department,
        IReadOnlyCollection<CurrentAccrualBalance> accrualBalances,
        IReadOnlyCollection<FacultyLeaveRequestResponse> requests,
        IReadOnlyCollection<LeaveType> leaveTypes,
        int pendingCount,
        int approvedCount)
    {
        var balanceSummary = BuildBalanceSummary(accrualBalances);

        return new FacultyDashboardResponse(
            Faculty: new FacultyProfileResponse(
                IamId: iamId,
                EmployeeId: employee?.EmployeeId?.Trim() ?? appUser.EmployeeId?.Trim(),
                Name: employee?.DisplayName ?? appUser.DisplayName ?? iamId,
                Email: employee?.Email ?? appUser.Email,
                DepartmentCode: employee?.ResolvedReportingDepartmentCode,
                DepartmentName: employee?.ResolvedReportingDepartmentName,
                WorkflowMode: department?.WorkflowMode.ToString(),
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
            RecentRequests: requests,
            LeaveTypes: BuildLeaveTypeResponses(leaveTypes));
    }

    private async Task<List<FacultyLeaveRequestResponse>> GetLeaveRequestsAsync(
        int appUserId,
        int? limit,
        CancellationToken cancellationToken)
    {
        IQueryable<LeaveRequest> query = _db.LeaveRequests
            .AsNoTracking()
            .Where(request => request.AppUserId == appUserId)
            .OrderByDescending(request => request.SubmittedAt)
            .ThenByDescending(request => request.Id);

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var requests = await query.ToListAsync(cancellationToken);

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
            .Select(request => CreateFacultyLeaveRequestResponse(request, leaveTypesById))
            .ToList();
    }

    private async Task<FacultyLeaveRequestResponse> BuildFacultyLeaveRequestResponseAsync(
        LeaveRequest request,
        CancellationToken cancellationToken)
    {
        var leaveTypeIds = new[] { request.LeaveTypeId, request.PayLeaveTypeId ?? 0 }
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var leaveTypesById = await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => leaveTypeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, cancellationToken);

        return CreateFacultyLeaveRequestResponse(request, leaveTypesById);
    }

    private static FacultyLeaveRequestResponse CreateFacultyLeaveRequestResponse(
        LeaveRequest request,
        IReadOnlyDictionary<int, LeaveType> leaveTypesById)
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
            DepartmentName: request.ReportingDepartmentNameSnapshot,
            Note: request.Note);
    }

    private async Task<List<LeaveType>> GetLeaveTypesAsync(CancellationToken cancellationToken)
    {
        return await _db.LeaveTypes
            .AsNoTracking()
            .Where(type => type.IsActive)
            .OrderBy(type => type.DisplayName)
            .ToListAsync(cancellationToken);
    }

    private async Task<(int PendingCount, int ApprovedCount)> GetRequestSnapshotCountsAsync(
        string iamId,
        CancellationToken cancellationToken)
    {
        var statusCounts = await _db.LeaveRequests
            .AsNoTracking()
            .Where(request =>
                request.IamId == iamId &&
                (request.Status == LeaveRequestStatus.PendingApproval ||
                    request.Status == LeaveRequestStatus.Approved))
            .GroupBy(request => request.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var pendingCount = statusCounts
            .Where(entry => entry.Status == LeaveRequestStatus.PendingApproval)
            .Sum(entry => entry.Count);
        var approvedCount = statusCounts
            .Where(entry => entry.Status == LeaveRequestStatus.Approved)
            .Sum(entry => entry.Count);

        return (pendingCount, approvedCount);
    }

    private static List<FacultyLeaveTypeResponse> BuildLeaveTypeResponses(
        IReadOnlyCollection<LeaveType> leaveTypes)
    {
        var responses = new List<FacultyLeaveTypeResponse>();

        foreach (var label in DesiredLeaveTypeLabels)
        {
            var matchingType = leaveTypes.FirstOrDefault(type =>
                string.Equals(GetCanonicalLeaveTypeLabel(type), label, StringComparison.Ordinal));

            if (matchingType != null)
            {
                responses.Add(new FacultyLeaveTypeResponse(
                    Id: matchingType.Id,
                    DisplayName: label,
                    HasAccrualBalance: matchingType.HasAccrualBalance));
            }
        }

        return responses;
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

    private async Task<HashSet<int>> GetActiveCaoClusterIdsAsync(
        string iamId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (await _db.ClusterCaoAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.IamId.Trim() == iamId.Trim() &&
                    assignment.ClosedUtc == null &&
                    assignment.EffectiveStartDate <= today &&
                    (!assignment.EffectiveEndDateExclusive.HasValue ||
                        assignment.EffectiveEndDateExclusive > today))
                .Select(assignment => assignment.ClusterId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private async Task<HashSet<string>> GetActiveChairDepartmentCodesAsync(
        string iamId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (await _db.DepartmentChairAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.IamId.Trim() == iamId.Trim() &&
                    assignment.ClosedUtc == null &&
                    assignment.EffectiveStartDate <= today &&
                    (!assignment.EffectiveEndDateExclusive.HasValue ||
                        assignment.EffectiveEndDateExclusive > today))
                .Select(assignment => assignment.DepartmentCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string[]> ValidateRequestShape(
        CreateFacultyLeaveRequest request,
        LeaveType leaveType)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var allowsZeroHours =
            string.Equals(
                leaveType.LeaveTypeKey,
                ProfessionalDevelopmentLeaveTypeKey,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                leaveType.LeaveTypeKey,
                SabbaticalLeaveTypeKey,
                StringComparison.OrdinalIgnoreCase);

        if (request.LeaveTypeId <= 0)
        {
            errors["leaveTypeId"] = ["Select a leave type."];
        }

        if (request.EndDate < request.StartDate)
        {
            errors["endDate"] = ["End date must be on or after the start date."];
        }

        if (request.TotalHours < 0)
        {
            errors["totalHours"] = ["Total hours cannot be negative."];
        }
        else if (!allowsZeroHours && request.TotalHours == 0)
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

    private static string GetCanonicalLeaveTypeLabel(LeaveType leaveType)
    {
        if (string.Equals(
                leaveType.LeaveTypeKey,
                FamilyCareLeaveTypeKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return FmlaLeaveTypeLabel;
        }

        if (string.Equals(
                leaveType.LeaveTypeKey,
                ProfessionalDevelopmentLeaveTypeKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProfessionalDevelopmentLeaveTypeLabel;
        }

        if (string.Equals(
                leaveType.LeaveTypeKey,
                SabbaticalLeaveTypeKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return SabbaticalLeaveTypeLabel;
        }

        return leaveType.DisplayName;
    }

    private static bool HasRole(ClaimsPrincipal principal, string role)
    {
        return principal.FindAll(ClaimTypes.Role).Any(roleClaim =>
            string.Equals(roleClaim.Value, role, StringComparison.OrdinalIgnoreCase));
    }

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
    string? WorkflowMode,
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
    string DepartmentName,
    string? Note);

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

public sealed record FacultyDashboardViewerResult(
    FacultyDashboardResponse? Dashboard,
    bool IsForbidden,
    bool ViewerMissing,
    bool TargetMissing)
{
    public static FacultyDashboardViewerResult Success(FacultyDashboardResponse dashboard) =>
        new(dashboard, false, false, false);

    public static FacultyDashboardViewerResult Forbidden() =>
        new(null, true, false, false);

    public static FacultyDashboardViewerResult ViewerNotFound() =>
        new(null, false, true, false);

    public static FacultyDashboardViewerResult TargetNotFound() =>
        new(null, false, false, true);
}
