using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Core.Data;
using Server.Core.Domain;

namespace Server.Core.Notification;

public static class LeaveRequestNotificationTypes
{
    public const string SubmittedForApproval = "LeaveRequestSubmittedForApproval";
    public const string AutoApproved = "LeaveRequestAutoApproved";
    public const string Approved = "LeaveRequestApproved";
    public const string Denied = "LeaveRequestDenied";
}

public interface ILeaveRequestNotificationQueue
{
    Task QueueSubmissionAsync(LeaveRequest request, AppUser requester, CancellationToken cancellationToken);
    void QueueDecision(LeaveRequest request, AppUser requester);
}

/// <summary>
/// Adds leave-request notifications to the same DbContext transaction as the business action.
/// Callers own SaveChanges and commit before waking the delivery worker.
/// </summary>
public sealed class LeaveRequestNotificationQueue : ILeaveRequestNotificationQueue
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly AppDbContext _db;
    private readonly ILogger<LeaveRequestNotificationQueue> _logger;

    public LeaveRequestNotificationQueue(
        AppDbContext db,
        ILogger<LeaveRequestNotificationQueue> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task QueueSubmissionAsync(
        LeaveRequest request,
        AppUser requester,
        CancellationToken cancellationToken)
    {
        if (request.WorkflowModeSnapshot == WorkflowMode.ApprovalRequired)
        {
            var routingEmails = await _db.DepartmentEmailRoutings
                .AsNoTracking()
                .Where(routing => routing.DepartmentCode == request.ReportingDepartmentCodeSnapshot && routing.IsActive)
                .Select(routing => routing.ToEmail)
                .ToListAsync(cancellationToken);

            foreach (var recipient in routingEmails.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Queue(request, recipient, LeaveRequestNotificationTypes.SubmittedForApproval);
            }

            return;
        }

        Queue(request, requester.Email, LeaveRequestNotificationTypes.AutoApproved);
    }

    public void QueueDecision(LeaveRequest request, AppUser requester)
    {
        var notificationType = request.Status switch
        {
            LeaveRequestStatus.Approved => LeaveRequestNotificationTypes.Approved,
            LeaveRequestStatus.Denied => LeaveRequestNotificationTypes.Denied,
            _ => null,
        };

        if (notificationType is not null)
        {
            Queue(request, requester.Email, notificationType);
        }
    }

    private void Queue(LeaveRequest request, string? recipientEmail, string notificationType)
    {
        var email = recipientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !EmailValidator.IsValid(email))
        {
            _logger.LogWarning(
                "Skipping leave-request email with an invalid recipient. LeaveRequestId={LeaveRequestId} NotificationType={NotificationType}",
                request.Id,
                notificationType);
            return;
        }

        var dedupeKey = $"leave-request:{request.Id}:{notificationType}:{email.ToLowerInvariant()}";
        if (_db.OutboundMessages.Local.Any(message => message.DedupeKey.Equals(dedupeKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _db.OutboundMessages.Add(new OutboundMessage
        {
            LeaveRequestId = request.Id,
            NotificationType = notificationType,
            RecipientEmail = email,
            Status = OutboundMessageStatus.Pending,
            DedupeKey = dedupeKey,
            NotBeforeUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
        });
    }
}

public interface IEmailDeliveryWakeSignal
{
    void Signal();
    ValueTask WaitAsync(CancellationToken cancellationToken);
}

public sealed class EmailDeliveryWakeSignal : IEmailDeliveryWakeSignal
{
    private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(1);

    public void Signal() => _signals.Writer.TryWrite(true);

    public async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        await _signals.Reader.ReadAsync(cancellationToken);
    }
}

public sealed record LeaveRequestEmailDeliveryResult(int ClaimedCount, int SentCount, int RetryCount, int DeadLetterCount);

public interface ILeaveRequestEmailDeliveryService
{
    Task<LeaveRequestEmailDeliveryResult> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Claims queued messages using a database lease, delivers them through the existing
/// notification renderer/SMTP service, and records a durable outcome.
/// </summary>
public sealed class LeaveRequestEmailDeliveryService : ILeaveRequestEmailDeliveryService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<LeaveRequestEmailDeliveryService> _logger;

    public LeaveRequestEmailDeliveryService(
        AppDbContext db,
        INotificationService notificationService,
        IOptions<EmailDeliveryOptions> options,
        ILogger<LeaveRequestEmailDeliveryService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LeaveRequestEmailDeliveryResult> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var claimed = await ClaimDueAsync(nowUtc, cancellationToken);
        var sent = 0;
        var retry = 0;
        var deadLetter = 0;

        foreach (var message in claimed)
        {
            try
            {
                var notification = await CreateNotificationAsync(message, cancellationToken);
                if (notification is null)
                {
                    await MarkDeadLetterAsync(message, "The related leave request could not be loaded.", cancellationToken);
                    deadLetter++;
                    continue;
                }

                await _notificationService.SendAsync(
                    new EmailRecipients { To = [message.RecipientEmail] },
                    notification.Subject,
                    notification.Header,
                    notification.Message,
                    cancellationToken);

                if (await MarkSentAsync(message, nowUtc, cancellationToken))
                {
                    sent++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var isDeadLetter = await MarkFailureAsync(message, nowUtc, ex, cancellationToken);
                if (isDeadLetter)
                {
                    deadLetter++;
                }
                else
                {
                    retry++;
                }
            }
        }

        return new LeaveRequestEmailDeliveryResult(claimed.Count, sent, retry, deadLetter);
    }

    private async Task<List<ClaimedOutboundMessage>> ClaimDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (_options.BatchSize <= 0 || _options.LockDurationMinutes <= 0)
        {
            throw new InvalidOperationException("EmailDelivery:BatchSize and EmailDelivery:LockDurationMinutes must be greater than zero.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lockedUntilUtc = nowUtc.AddMinutes(_options.LockDurationMinutes);
        var messages = await _db.OutboundMessages
            .Where(message =>
                (message.Status == OutboundMessageStatus.Pending || message.Status == OutboundMessageStatus.Failed) &&
                message.NotBeforeUtc <= nowUtc &&
                (message.LockedUntilUtc == null || message.LockedUntilUtc <= nowUtc))
            .OrderBy(message => message.NotBeforeUtc)
            .ThenBy(message => message.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.LockId = Guid.NewGuid();
            message.LockedUntilUtc = lockedUntilUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        return messages
            .Select(message => new ClaimedOutboundMessage(message.Id, message.LeaveRequestId, message.NotificationType, message.RecipientEmail, message.LockId!.Value, message.AttemptCount))
            .ToList();
    }

    private async Task<LeaveRequestEmailNotification?> CreateNotificationAsync(ClaimedOutboundMessage message, CancellationToken cancellationToken)
    {
        var request = await _db.LeaveRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == message.LeaveRequestId, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var requester = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == request.AppUserId, cancellationToken);
        var leaveType = await _db.LeaveTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(type => type.Id == request.LeaveTypeId, cancellationToken);
        var requesterName = requester?.DisplayName ?? request.IamId;
        var leaveTypeName = leaveType?.DisplayName ?? "leave";
        var dates = request.StartDate == request.EndDate
            ? request.StartDate.ToString("MMMM d, yyyy")
            : $"{request.StartDate:MMMM d, yyyy} through {request.EndDate:MMMM d, yyyy}";

        return message.NotificationType switch
        {
            LeaveRequestNotificationTypes.SubmittedForApproval => new LeaveRequestEmailNotification(
                "Leave request awaiting approval",
                "Leave request awaiting approval",
                $"{requesterName} submitted a {leaveTypeName} request for {dates} ({request.TotalHours:0.##} hours)."),
            LeaveRequestNotificationTypes.AutoApproved => new LeaveRequestEmailNotification(
                "Leave request approved",
                "Your leave request was approved",
                $"Your {leaveTypeName} request for {dates} ({request.TotalHours:0.##} hours) was automatically approved."),
            LeaveRequestNotificationTypes.Approved => new LeaveRequestEmailNotification(
                "Leave request approved",
                "Your leave request was approved",
                $"Your {leaveTypeName} request for {dates} ({request.TotalHours:0.##} hours) was approved."),
            LeaveRequestNotificationTypes.Denied => new LeaveRequestEmailNotification(
                "Leave request denied",
                "Your leave request was denied",
                $"Your {leaveTypeName} request for {dates} ({request.TotalHours:0.##} hours) was denied."),
            _ => null,
        };
    }

    private Task<bool> MarkSentAsync(ClaimedOutboundMessage message, DateTime nowUtc, CancellationToken cancellationToken)
    {
        return UpdateClaimAsync(
            message,
            row =>
            {
                row.Status = OutboundMessageStatus.Sent;
                row.SentUtc = nowUtc;
                row.LockId = null;
                row.LockedUntilUtc = null;
                row.LastError = null;
            },
            cancellationToken);
    }

    private async Task<bool> MarkFailureAsync(ClaimedOutboundMessage message, DateTime nowUtc, Exception exception, CancellationToken cancellationToken)
    {
        var nextAttempt = message.AttemptCount + 1;
        var deadLetter = nextAttempt >= _options.MaxAttempts;
        var error = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
        var retryAt = nowUtc.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(nextAttempt, 6))));

        var updated = await UpdateClaimAsync(
            message,
            row =>
            {
                row.Status = deadLetter ? OutboundMessageStatus.DeadLetter : OutboundMessageStatus.Failed;
                row.AttemptCount = nextAttempt;
                row.NotBeforeUtc = retryAt;
                row.LockId = null;
                row.LockedUntilUtc = null;
                row.LastError = error;
            },
            cancellationToken);

        if (updated)
        {
            _logger.LogWarning(
                exception,
                "Leave-request email delivery {Outcome}. OutboundMessageId={OutboundMessageId} NotificationType={NotificationType}",
                deadLetter ? "dead-lettered" : "will retry",
                message.Id,
                message.NotificationType);
        }

        return deadLetter;
    }

    private async Task MarkDeadLetterAsync(ClaimedOutboundMessage message, string error, CancellationToken cancellationToken)
    {
        await UpdateClaimAsync(
            message,
            row =>
            {
                row.Status = OutboundMessageStatus.DeadLetter;
                row.AttemptCount = message.AttemptCount + 1;
                row.LockId = null;
                row.LockedUntilUtc = null;
                row.LastError = error;
            },
            cancellationToken);
    }

    private async Task<bool> UpdateClaimAsync(
        ClaimedOutboundMessage message,
        Action<OutboundMessage> update,
        CancellationToken cancellationToken)
    {
        var row = await _db.OutboundMessages.SingleOrDefaultAsync(
            row => row.Id == message.Id && row.LockId == message.LockId,
            cancellationToken);
        if (row is null)
        {
            return false;
        }

        update(row);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private sealed record ClaimedOutboundMessage(
        int Id,
        int LeaveRequestId,
        string NotificationType,
        string RecipientEmail,
        Guid LockId,
        int AttemptCount);

    private sealed record LeaveRequestEmailNotification(string Subject, string Header, string Message);
}
