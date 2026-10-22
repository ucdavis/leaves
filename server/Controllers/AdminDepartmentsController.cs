using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Core.Notification;
using Server.Helpers;
using Server.Services;

namespace Server.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("api/admin/departments")]
public sealed class AdminDepartmentsController : ApiControllerBase
{
    private const int ClusterNameMaxLength = 100;
    private const int DepartmentCodeMaxLength = 10;
    private const int DepartmentNameMaxLength = 100;
    private readonly AdminDirectoryDataService _adminDirectoryDataService;
    private readonly AdminDirectoryService _adminDirectoryService;
    private readonly AppDbContext _db;
    private readonly ILeaveRequestNotificationQueue _notificationQueue;
    private readonly IEmailDeliveryWakeSignal _emailDeliveryWakeSignal;
    private readonly IUserService _userService;
    private readonly IHostEnvironment _environment;

    public AdminDepartmentsController(
        AppDbContext db,
        AdminDirectoryDataService adminDirectoryDataService,
        AdminDirectoryService adminDirectoryService,
        ILeaveRequestNotificationQueue notificationQueue,
        IEmailDeliveryWakeSignal emailDeliveryWakeSignal,
        IUserService userService,
        IHostEnvironment environment)
    {
        _db = db;
        _adminDirectoryDataService = adminDirectoryDataService;
        _adminDirectoryService = adminDirectoryService;
        _notificationQueue = notificationQueue;
        _emailDeliveryWakeSignal = emailDeliveryWakeSignal;
        _userService = userService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        return Ok(await _adminDirectoryService.GetDepartmentsAsync(cancellationToken));
    }

    [HttpGet("{departmentCode}/faculty")]
    public async Task<IActionResult> GetDepartmentFacultyAsync(string departmentCode, CancellationToken cancellationToken)
    {
        var exists = await _db.Departments.AnyAsync(
            department => department.DepartmentCode == departmentCode, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        return Ok(await _adminDirectoryService.GetDepartmentFacultyAsync(departmentCode, cancellationToken));
    }

    [HttpGet("cao-candidates")]
    public async Task<IActionResult> SearchCaoCandidatesAsync([FromQuery] string? query, CancellationToken cancellationToken)
    {
        return Ok(await _adminDirectoryService.SearchCaoCandidatesAsync(query, cancellationToken));
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetDepartmentOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = await _adminDirectoryDataService.GetDepartmentOptionsAsync(cancellationToken);
            return Ok(options);
        }
        catch (Exception exception)
        {
            var detail = _environment.IsDevelopment()
                ? exception.GetBaseException().Message
                : "Unable to load department options.";
            return Problem(
                detail: detail,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unable to load department options");
        }
    }

    [HttpPost("clusters")]
    public async Task<IActionResult> CreateCluster([FromBody] CreateClusterRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationProblem("Cluster name is required.");
        }

        if (name.Length > ClusterNameMaxLength)
        {
            return ValidationProblem($"Cluster name must be {ClusterNameMaxLength} characters or fewer.");
        }

        var existingCluster = await _db.Clusters.IgnoreQueryFilters().FirstOrDefaultAsync(
            cluster => cluster.ClusterName == name,
            cancellationToken);
        if (existingCluster != null)
        {
            if (!existingCluster.IsActive)
            {
                return Conflict(new { inactiveClusterId = existingCluster.Id });
            }

            return Conflict("A cluster with that name already exists.");
        }

        var now = DateTime.UtcNow;
        var createdByAppUserId = await GetAuthenticatedAppUserId(cancellationToken);

        _db.Clusters.Add(new Cluster
        {
            ClusterName = name,
            CreatedByAppUserId = createdByAppUserId,
            CreatedUtc = now,
            IsActive = true,
            UpdatedUtc = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("clusters/{clusterId:int}")]
    public async Task<IActionResult> UpdateCluster(int clusterId, [FromBody] UpdateClusterRequest request, CancellationToken cancellationToken)
    {
        var cluster = await _db.Clusters.FirstOrDefaultAsync(
            item => item.Id == clusterId,
            cancellationToken);

        if (cluster == null || !cluster.IsActive)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (name.Length > ClusterNameMaxLength)
            {
                return ValidationProblem($"Cluster name must be {ClusterNameMaxLength} characters or fewer.");
            }

            var duplicateExists = await _db.Clusters.AnyAsync(
                item => item.Id != clusterId && item.IsActive && item.ClusterName == name,
                cancellationToken);
            if (duplicateExists)
            {
                return Conflict("A cluster with that name already exists.");
            }

            cluster.ClusterName = name;
        }

        if (request.CaoUserIdSet)
        {
            var caoUpdateResult = await UpdateClusterCaoAssignmentAsync(
                cluster.Id,
                request.CaoUserId,
                cancellationToken);
            if (caoUpdateResult != null)
            {
                return caoUpdateResult;
            }
        }

        cluster.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("clusters/{clusterId:int}")]
    public async Task<IActionResult> DeleteCluster(int clusterId, CancellationToken cancellationToken)
    {
        var cluster = await _db.Clusters
            .Include(item => item.Departments)
            .FirstOrDefaultAsync(item => item.Id == clusterId, cancellationToken);

        if (cluster == null)
        {
            return NotFound();
        }

        var caoUpdateResult = await UpdateClusterCaoAssignmentAsync(cluster.Id, null, cancellationToken);
        if (caoUpdateResult != null)
        {
            return caoUpdateResult;
        }

        var now = DateTime.UtcNow;
        foreach (var department in cluster.Departments.ToList())
        {
            department.ClusterId = null;
            department.UpdatedUtc = now;
        }

        cluster.IsActive = false;
        cluster.UpdatedUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("clusters/{clusterId:int}/reactivate")]
    public async Task<IActionResult> ReactivateCluster(int clusterId, CancellationToken cancellationToken)
    {
        var cluster = await _db.Clusters.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == clusterId, cancellationToken);
        if (cluster == null || cluster.IsActive)
        {
            return NotFound();
        }

        var duplicateExists = await _db.Clusters.AnyAsync(
            item => item.Id != clusterId && item.IsActive && item.ClusterName == cluster.ClusterName,
            cancellationToken);
        if (duplicateExists)
        {
            return Conflict("A cluster with that name already exists.");
        }

        cluster.IsActive = true;
        cluster.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var departmentCode = request.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(departmentCode))
        {
            return ValidationProblem("Department code is required.");
        }

        if (departmentCode.Length > DepartmentCodeMaxLength)
        {
            return ValidationProblem($"Department code must be {DepartmentCodeMaxLength} characters or fewer.");
        }

        if (!departmentCode.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-'))
        {
            return ValidationProblem("Department code must use only letters, numbers, underscores, or hyphens.");
        }

        var departmentName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            return ValidationProblem("Department name is required.");
        }

        if (departmentName.Length > DepartmentNameMaxLength)
        {
            return ValidationProblem($"Department name must be {DepartmentNameMaxLength} characters or fewer.");
        }

        if (!request.ClusterId.HasValue)
        {
            return ValidationProblem("Cluster is required.");
        }

        var cluster = await _db.Clusters.FirstOrDefaultAsync(
            item => item.Id == request.ClusterId.Value,
            cancellationToken);
        if (cluster == null || !cluster.IsActive)
        {
            return ValidationProblem("Selected cluster does not exist.");
        }

        var existingDepartment = await _db.Departments.IgnoreQueryFilters().FirstOrDefaultAsync(
            department => department.DepartmentCode == departmentCode,
            cancellationToken);
        if (existingDepartment != null)
        {
            if (!existingDepartment.IsActive)
            {
                return Conflict(new { inactiveDepartmentCode = existingDepartment.DepartmentCode });
            }

            return Conflict("A department with that code already exists.");
        }

        var isResolvedDepartmentCode = await _adminDirectoryDataService.IsResolvedDepartmentCodeAsync(
            departmentCode,
            cancellationToken);
        if (!isResolvedDepartmentCode)
        {
            return ValidationProblem("Department code must exist in the current faculty data.");
        }

        if (!TryGetWorkflowMode(request.ApprovalMode, defaultToAuto: true, out var workflowMode))
        {
            return ValidationProblem("Approval mode must be auto or approval.");
        }

        var now = DateTime.UtcNow;
        _db.Departments.Add(new Department
        {
            ClusterId = request.ClusterId,
            CreatedUtc = now,
            DepartmentCode = departmentCode,
            DepartmentName = departmentName,
            IsActive = true,
            UpdatedUtc = now,
            WorkflowMode = workflowMode,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("{departmentCode}")]
    public async Task<IActionResult> UpdateDepartment(string departmentCode, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode,
            cancellationToken);

        if (department == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            department.DepartmentName = request.Name.Trim();
        }

        if (request.ClusterIdSet)
        {
            if (request.ClusterId.HasValue)
            {
                var cluster = await _db.Clusters.FirstOrDefaultAsync(
                    item => item.Id == request.ClusterId.Value,
                    cancellationToken);
                if (cluster == null || !cluster.IsActive)
                {
                    return ValidationProblem("Selected cluster does not exist.");
                }
            }

            department.ClusterId = request.ClusterId;
        }

        var approvedPendingRequests = false;
        if (!string.IsNullOrWhiteSpace(request.ApprovalMode))
        {
            if (!TryGetWorkflowMode(request.ApprovalMode, defaultToAuto: false, out var workflowMode))
            {
                return ValidationProblem("Approval mode must be auto or approval.");
            }

            var pendingRequestCount = await _db.LeaveRequests.CountAsync(
                leaveRequest =>
                    leaveRequest.ReportingDepartmentCodeSnapshot == department.DepartmentCode &&
                    leaveRequest.Status == LeaveRequestStatus.PendingApproval,
                cancellationToken);
            if (department.WorkflowMode == WorkflowMode.ApprovalRequired &&
                workflowMode == WorkflowMode.DirectSubmission &&
                pendingRequestCount > 0 &&
                !request.ApprovePendingRequests)
            {
                return Conflict($"Switching to Auto-approve will approve all {pendingRequestCount} pending requests for this department. Confirm the change before saving.");
            }

            AppUser? actor = null;
            if (workflowMode == WorkflowMode.DirectSubmission && pendingRequestCount > 0)
            {
                actor = await GetAuthenticatedAppUserAsync(cancellationToken);
                if (actor == null)
                {
                    return ValidationProblem("The authenticated admin must have an AppUser row before pending requests can be approved.");
                }
            }

            var now = DateTime.UtcNow;
            approvedPendingRequests = await ApplyWorkflowModeToPendingRequestsAsync(
                department,
                workflowMode,
                actor,
                now,
                cancellationToken);
        }

        if (request.ChairUserIdSet)
        {
            var chairUpdateResult = await UpdateDepartmentChairAssignmentAsync(
                department.DepartmentCode,
                request.ChairUserId,
                cancellationToken);
            if (chairUpdateResult != null)
            {
                return chairUpdateResult;
            }
        }

        department.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (approvedPendingRequests)
        {
            _emailDeliveryWakeSignal.Signal();
        }

        return NoContent();
    }

    [HttpDelete("{departmentCode}")]
    public async Task<IActionResult> DeleteDepartment(string departmentCode, CancellationToken cancellationToken)
    {
        var department = await _db.Departments
            .FirstOrDefaultAsync(item => item.DepartmentCode == departmentCode, cancellationToken);

        if (department == null || !department.IsActive)
        {
            return NotFound();
        }

        var chairUpdateResult = await UpdateDepartmentChairAssignmentAsync(
            department.DepartmentCode,
            null,
            cancellationToken);
        if (chairUpdateResult != null)
        {
            return chairUpdateResult;
        }

        department.IsActive = false;
        department.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{departmentCode}/reactivate")]
    public async Task<IActionResult> ReactivateDepartment(
        string departmentCode,
        [FromBody] ReactivateDepartmentRequest? request,
        CancellationToken cancellationToken)
    {
        var department = await _db.Departments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.DepartmentCode == departmentCode, cancellationToken);
        if (department == null || department.IsActive)
        {
            return NotFound();
        }

        if (request == null || !request.ClusterId.HasValue)
        {
            return ValidationProblem("Cluster is required.");
        }

        var clusterId = request.ClusterId.Value;
        var cluster = await _db.Clusters.FirstOrDefaultAsync(
            item => item.Id == clusterId,
            cancellationToken);
        if (cluster == null)
        {
            return ValidationProblem("Selected cluster does not exist.");
        }

        department.ClusterId = cluster.Id;
        department.IsActive = true;
        department.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{departmentCode}/routing-emails")]
    public async Task<IActionResult> AddRoutingEmail(string departmentCode, [FromBody] UpsertRoutingEmailRequest request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode,
            cancellationToken);

        if (department == null || !department.IsActive)
        {
            return NotFound();
        }

        var email = request.Address?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ValidationProblem("Email address is required.");
        }

        var existing = await _db.DepartmentEmailRoutings.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode && item.ToEmail == email,
            cancellationToken);

        if (existing == null)
        {
            var adminUserId = await GetAuthenticatedAppUserId(cancellationToken);
            if (adminUserId == null)
            {
                return ValidationProblem("The authenticated admin must have an AppUser row before routing emails can be updated.");
            }

            existing = new DepartmentEmailRouting
            {
                DepartmentCode = departmentCode,
                IsActive = true,
                ToEmail = email,
                UpdatedByAppUserId = adminUserId.Value,
                UpdatedUtc = DateTime.UtcNow,
            };

            _db.DepartmentEmailRoutings.Add(existing);
        }
        else
        {
            existing.IsActive = true;
            existing.UpdatedUtc = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateDepartmentRoutingEmail(ex))
        {
            return Conflict("That routing email already exists for this department.");
        }

        return NoContent();
    }

    [HttpDelete("{departmentCode}/routing-emails/{id:int}")]
    public async Task<IActionResult> RemoveRoutingEmail(string departmentCode, int id, CancellationToken cancellationToken)
    {
        var routing = await _db.DepartmentEmailRoutings.FirstOrDefaultAsync(
            item => item.Id == id && item.DepartmentCode == departmentCode,
            cancellationToken);

        if (routing == null)
        {
            return NotFound();
        }

        _db.DepartmentEmailRoutings.Remove(routing);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<int?> GetAuthenticatedAppUserId(CancellationToken cancellationToken) =>
        (await GetAuthenticatedAppUserAsync(cancellationToken))?.Id;

    private async Task<AppUser?> GetAuthenticatedAppUserAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId) || !Guid.TryParse(userId, out var entraObjectId))
        {
            return null;
        }

        var appUser = await _db.AppUsers
            .FirstOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);

        if (appUser != null)
        {
            return appUser;
        }

        await _userService.EnsureUserProfileAsync(
            User,
            recordSignIn: false,
            cancellationToken: cancellationToken);

        return await _db.AppUsers
            .FirstOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);
    }

    private async Task<bool> ApplyWorkflowModeToPendingRequestsAsync(
        Department department,
        WorkflowMode workflowMode,
        AppUser? actor,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var pendingRequests = await _db.LeaveRequests
            .Where(request =>
                request.ReportingDepartmentCodeSnapshot == department.DepartmentCode &&
                request.Status == LeaveRequestStatus.PendingApproval)
            .ToListAsync(cancellationToken);

        department.WorkflowMode = workflowMode;
        foreach (var request in pendingRequests)
        {
            request.WorkflowModeSnapshot = workflowMode;
            request.UpdatedUtc = nowUtc;
        }

        if (workflowMode != WorkflowMode.DirectSubmission || pendingRequests.Count == 0)
        {
            return false;
        }

        if (actor == null)
        {
            throw new InvalidOperationException("An AppUser actor is required to approve pending leave requests.");
        }

        var requesterIds = pendingRequests.Select(request => request.AppUserId).Distinct().ToArray();
        var requesters = await _db.AppUsers
            .Where(user => requesterIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        foreach (var request in pendingRequests)
        {
            if (!requesters.TryGetValue(request.AppUserId, out var requester))
            {
                continue;
            }

            request.Status = LeaveRequestStatus.Approved;
            _db.LeaveRequestActions.Add(new LeaveRequestAction
            {
                LeaveRequestId = request.Id,
                ActionType = LeaveRequestActionType.Approved,
                ActorAppUserId = actor.Id,
                ActorIamId = actor.IamId,
                ActionAt = nowUtc,
                Comment = "Approved because the department approval mode changed to Auto-approve.",
                IsSelfAction = false,
            });
            await _notificationQueue.QueueDecisionAsync(request, requester, cancellationToken);
        }

        return true;
    }

    private static bool TryGetWorkflowMode(
        string? approvalMode,
        bool defaultToAuto,
        out WorkflowMode workflowMode)
    {
        switch (approvalMode?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
                workflowMode = defaultToAuto ? WorkflowMode.DirectSubmission : default;
                return defaultToAuto;
            case "approval":
                workflowMode = WorkflowMode.ApprovalRequired;
                return true;
            case "auto":
                workflowMode = WorkflowMode.DirectSubmission;
                return true;
            default:
                workflowMode = default;
                return false;
        }
    }

    private async Task<IActionResult?> UpdateClusterCaoAssignmentAsync(
        int clusterId,
        string? caoUserId,
        CancellationToken cancellationToken)
    {
        var adminUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (adminUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before cluster CAOs can be updated.");
        }

        var normalizedCaoUserId = caoUserId?.Trim();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentAssignments = await _db.ClusterCaoAssignments
            .Where(item => item.ClusterId == clusterId &&
                           item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(normalizedCaoUserId))
        {
            foreach (var currentAssignment in currentAssignments)
            {
                AdminRolesService.CloseClusterCaoAssignment(currentAssignment, adminUserId.Value, now, today);
            }

            return null;
        }

        var userExists = await _adminDirectoryDataService.DirectoryUserExistsAsync(
            normalizedCaoUserId,
            cancellationToken);
        if (!userExists)
        {
            return ValidationProblem("Selected CAO must be a valid directory user.");
        }

        var matchingAssignment = currentAssignments.FirstOrDefault(currentAssignment =>
            string.Equals(currentAssignment.IamId, normalizedCaoUserId, StringComparison.OrdinalIgnoreCase));
        if (matchingAssignment != null)
        {
            foreach (var currentAssignment in currentAssignments.Where(currentAssignment =>
                         currentAssignment.Id != matchingAssignment.Id))
            {
                AdminRolesService.CloseClusterCaoAssignment(currentAssignment, adminUserId.Value, now, today);
            }

            return null;
        }

        foreach (var currentAssignment in currentAssignments)
        {
            AdminRolesService.CloseClusterCaoAssignment(currentAssignment, adminUserId.Value, now, today);
        }

        _db.ClusterCaoAssignments.Add(new ClusterCaoAssignment
        {
            ClusterId = clusterId,
            CreatedByAppUserId = adminUserId.Value,
            CreatedUtc = now,
            EffectiveStartDate = today,
            IamId = normalizedCaoUserId,
        });

        return null;
    }

    private async Task<IActionResult?> UpdateDepartmentChairAssignmentAsync(
        string departmentCode,
        string? chairUserId,
        CancellationToken cancellationToken)
    {
        var adminUserId = await GetAuthenticatedAppUserId(cancellationToken);
        if (adminUserId == null)
        {
            return ValidationProblem("The authenticated admin must have an AppUser row before department chairs can be updated.");
        }

        var normalizedChairUserId = chairUserId?.Trim();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentAssignment = await _db.DepartmentChairAssignments
            .Where(item => item.DepartmentCode == departmentCode &&
                           item.ClosedUtc == null &&
                           item.EffectiveStartDate <= today &&
                           (!item.EffectiveEndDateExclusive.HasValue || item.EffectiveEndDateExclusive.Value > today))
            .OrderByDescending(item => item.EffectiveStartDate)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(normalizedChairUserId))
        {
            if (currentAssignment != null)
            {
                AdminRolesService.CloseDepartmentChairAssignment(currentAssignment, adminUserId.Value, now, today);
            }

            return null;
        }

        var isCurrentFacultyInDepartment = await _adminDirectoryDataService.IsCurrentFacultyInDepartmentAsync(
            normalizedChairUserId,
            departmentCode,
            cancellationToken);
        if (!isCurrentFacultyInDepartment)
        {
            return ValidationProblem("Selected chair must be a current faculty member in this department.");
        }

        if (currentAssignment != null &&
            string.Equals(currentAssignment.IamId, normalizedChairUserId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hasDuplicateActiveAssignment = await HasActiveDepartmentChairAssignmentAsync(
            departmentCode,
            normalizedChairUserId,
            today,
            cancellationToken,
            currentAssignment?.Id);
        if (hasDuplicateActiveAssignment)
        {
            return Conflict("That user already has an active department chair assignment for this department.");
        }

        if (currentAssignment != null)
        {
            AdminRolesService.CloseDepartmentChairAssignment(currentAssignment, adminUserId.Value, now, today);
        }

        _db.DepartmentChairAssignments.Add(new DepartmentChairAssignment
        {
            CreatedByAppUserId = adminUserId.Value,
            CreatedUtc = now,
            DepartmentCode = departmentCode,
            EffectiveStartDate = today,
            IamId = normalizedChairUserId,
        });

        return null;
    }

    private Task<bool> HasActiveDepartmentChairAssignmentAsync(
        string departmentCode,
        string iamId,
        DateOnly onDate,
        CancellationToken cancellationToken,
        int? excludeAssignmentId = null)
    {
        return _db.DepartmentChairAssignments.AnyAsync(
            assignment => assignment.DepartmentCode == departmentCode &&
                          assignment.ClosedUtc == null &&
                          assignment.IamId == iamId &&
                          assignment.EffectiveStartDate <= onDate &&
                          (!assignment.EffectiveEndDateExclusive.HasValue || assignment.EffectiveEndDateExclusive.Value > onDate) &&
                          (!excludeAssignmentId.HasValue || assignment.Id != excludeAssignmentId.Value),
            cancellationToken);
    }

    private static bool IsDuplicateDepartmentRoutingEmail(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    [HttpGet("{departmentCode}/pending-request-count")]
    public async Task<IActionResult> GetPendingRequestCount(
        string departmentCode,
        CancellationToken cancellationToken)
    {
        var departmentExists = await _db.Departments.AnyAsync(
            department => department.DepartmentCode == departmentCode,
            cancellationToken);
        if (!departmentExists)
        {
            return NotFound();
        }

        var pendingRequestCount = await _db.LeaveRequests.CountAsync(
            request =>
                request.ReportingDepartmentCodeSnapshot == departmentCode &&
                request.Status == LeaveRequestStatus.PendingApproval,
            cancellationToken);
        return Ok(new { pendingRequestCount });
    }

    public sealed record CreateClusterRequest(string? Name);
    public sealed record UpdateClusterRequest(string? Name, string? CaoUserId, bool CaoUserIdSet);
    public sealed record CreateDepartmentRequest(string? ApprovalMode, int? ClusterId, string? Code, string? Name);
    public sealed record ReactivateDepartmentRequest(int? ClusterId);
    public sealed record UpdateDepartmentRequest(string? Name, int? ClusterId, bool ClusterIdSet, string? ApprovalMode, bool ApprovePendingRequests, string? ChairUserId, bool ChairUserIdSet);
    public sealed record UpsertRoutingEmailRequest(string? Address);
}
