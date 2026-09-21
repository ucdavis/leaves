using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Services;

public sealed class AdminStatusDataService
{
    private readonly AppDbContext _db;

    public AdminStatusDataService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminStatusData> LoadStatusDataAsync(CancellationToken cancellationToken)
    {
        var pendingRequestCount = await _db.LeaveRequests
            .AsNoTracking()
            .CountAsync(request => request.Status == LeaveRequestStatus.PendingApproval, cancellationToken);
        var latestPeoplePromotionAt = await _db.People
            .Select(person => person.PromotedAt)
            .MaxAsync(cancellationToken);
        var latestAccrualUpdatedAt = await _db.EmployeeAccrualBalances
            .Select(row => (DateTime?)row.LastUpdated)
            .MaxAsync(cancellationToken);
        var accrualSummary = await _db.CurrentAccrualBalances
            .Where(balance => balance.TypeLabel.Contains("Vacation"))
            .GroupBy(_ => 1)
            .Select(group => new AccrualStatusSummary(
                group.Count(),
                group.Count(balance =>
                    balance.ApproachingMax == "Y" ||
                    balance.ApproachingMax == "Yes" ||
                    balance.ApproachingMax == "True"),
                group.Count(balance => balance.CalculatedBal >= balance.AccrualLimit)))
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminStatusData(
            AccrualSummary: accrualSummary ?? new AccrualStatusSummary(0, 0, 0),
            LatestAccrualUpdatedAt: latestAccrualUpdatedAt,
            LatestPeoplePromotionAt: latestPeoplePromotionAt,
            PendingRequestCount: pendingRequestCount);
    }
}

public sealed record AdminStatusData(
    AccrualStatusSummary AccrualSummary,
    DateTime? LatestAccrualUpdatedAt,
    DateTime? LatestPeoplePromotionAt,
    int PendingRequestCount);

public sealed record AccrualStatusSummary(
    int VacationBalanceCount,
    int ApproachingVacationCapCount,
    int FacultyAtVacationCapCount);
