using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaveRequestEmailDeliveryService> _logger;

    public LeaveRequestEmailDeliveryService(
        AppDbContext db,
        INotificationService notificationService,
        IOptions<EmailDeliveryOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<LeaveRequestEmailDeliveryService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<LeaveRequestEmailDeliveryResult> ProcessDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        ValidateOptions();

        var claimed = 0;
        var sent = 0;
        var retry = 0;
        var deadLetter = 0;

        while (claimed < _options.BatchSize)
        {
            var message = await ClaimNextDueAsync(nowUtc, cancellationToken);
            if (message is null)
            {
                break;
            }

            claimed++;

            await using var lease = await TryStartLeaseAsync(message, cancellationToken);
            if (lease is null)
            {
                LogOwnershipLost(message);
                continue;
            }

            try
            {
                var notification = await CreateNotificationAsync(message, cancellationToken);
                if (notification is null)
                {
                    if (await MarkDeadLetterAsync(message, "The related leave request could not be loaded.", cancellationToken))
                    {
                        deadLetter++;
                    }
                    else
                    {
                        LogOwnershipLost(message);
                    }

                    continue;
                }

                await _notificationService.SendAsync(
                    new EmailRecipients { To = [message.RecipientEmail] },
                    notification.Subject,
                    notification.Header,
                    notification.Message,
                    lease.CancellationToken);

                if (lease.HasLostOwnership)
                {
                    LogOwnershipLost(message);
                    continue;
                }

                if (await MarkSentAsync(message, DateTime.UtcNow, cancellationToken))
                {
                    sent++;
                }
                else
                {
                    LogOwnershipLost(message);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (lease.HasLostOwnership)
            {
                LogOwnershipLost(message);
            }
            catch (Exception ex)
            {
                if (lease.HasLostOwnership)
                {
                    LogOwnershipLost(message);
                    continue;
                }

                var failure = await MarkFailureAsync(message, nowUtc, ex, cancellationToken);
                if (!failure.Updated)
                {
                    LogOwnershipLost(message);
                    continue;
                }

                if (failure.IsDeadLetter)
                {
                    deadLetter++;
                }
                else
                {
                    retry++;
                }
            }
        }

        return new LeaveRequestEmailDeliveryResult(claimed, sent, retry, deadLetter);
    }

    private void ValidateOptions()
    {
        if (_options.BatchSize <= 0 || _options.LockDurationMinutes <= 0)
        {
            throw new InvalidOperationException("EmailDelivery:BatchSize and EmailDelivery:LockDurationMinutes must be greater than zero.");
        }

        if (_options.LeaseRenewalIntervalSeconds <= 0 ||
            _options.LeaseRenewalIntervalSeconds >= TimeSpan.FromMinutes(_options.LockDurationMinutes).TotalSeconds)
        {
            throw new InvalidOperationException("EmailDelivery:LeaseRenewalIntervalSeconds must be greater than zero and shorter than EmailDelivery:LockDurationMinutes.");
        }
    }

    private async Task<ClaimedOutboundMessage?> ClaimNextDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lockedUntilUtc = nowUtc.AddMinutes(_options.LockDurationMinutes);
        var messages = await _db.OutboundMessages
            .Where(message =>
                (message.Status == OutboundMessageStatus.Pending || message.Status == OutboundMessageStatus.Failed) &&
                message.NotBeforeUtc <= nowUtc &&
                (message.LockedUntilUtc == null || message.LockedUntilUtc <= nowUtc))
            .OrderBy(message => message.NotBeforeUtc)
            .ThenBy(message => message.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        var message = messages.SingleOrDefault();
        if (message is not null)
        {
            message.LockId = Guid.NewGuid();
            message.LockedUntilUtc = lockedUntilUtc;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        return message is null
            ? null
            : new ClaimedOutboundMessage(message.Id, message.LeaveRequestId, message.NotificationType, message.RecipientEmail, message.LockId!.Value, message.AttemptCount);
    }

    private async Task<DeliveryLease?> TryStartLeaseAsync(ClaimedOutboundMessage message, CancellationToken cancellationToken)
    {
        if (!await RenewLeaseAsync(message, cancellationToken))
        {
            return null;
        }

        return new DeliveryLease(this, message, cancellationToken);
    }

    private async Task<bool> RenewLeaseAsync(ClaimedOutboundMessage message, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nowUtc = DateTime.UtcNow;
        var row = await db.OutboundMessages.SingleOrDefaultAsync(
            row => row.Id == message.Id &&
                   row.LockId == message.LockId &&
                   row.LockedUntilUtc != null &&
                   row.LockedUntilUtc > nowUtc,
            cancellationToken);
        if (row is null)
        {
            return false;
        }

        row.LockedUntilUtc = nowUtc.AddMinutes(_options.LockDurationMinutes);
        await db.SaveChangesAsync(cancellationToken);
        return true;
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

    private async Task<FailureResult> MarkFailureAsync(ClaimedOutboundMessage message, DateTime nowUtc, Exception exception, CancellationToken cancellationToken)
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

        return new FailureResult(updated, deadLetter);
    }

    private Task<bool> MarkDeadLetterAsync(ClaimedOutboundMessage message, string error, CancellationToken cancellationToken)
    {
        return UpdateClaimAsync(
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
        var nowUtc = DateTime.UtcNow;
        var row = await _db.OutboundMessages.SingleOrDefaultAsync(
            row => row.Id == message.Id &&
                   row.LockId == message.LockId &&
                   row.LockedUntilUtc != null &&
                   row.LockedUntilUtc > nowUtc,
            cancellationToken);
        if (row is null)
        {
            return false;
        }

        update(row);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void LogOwnershipLost(ClaimedOutboundMessage message)
    {
        _logger.LogWarning(
            "Stopped leave-request email delivery because its lease was lost. OutboundMessageId={OutboundMessageId} NotificationType={NotificationType}",
            message.Id,
            message.NotificationType);
    }

    private sealed class DeliveryLease : IAsyncDisposable
    {
        private readonly LeaveRequestEmailDeliveryService _owner;
        private readonly ClaimedOutboundMessage _message;
        private readonly CancellationTokenSource _cancellation;
        private readonly Task _renewal;

        public DeliveryLease(
            LeaveRequestEmailDeliveryService owner,
            ClaimedOutboundMessage message,
            CancellationToken cancellationToken)
        {
            _owner = owner;
            _message = message;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _renewal = RenewAsync();
        }

        public CancellationToken CancellationToken => _cancellation.Token;

        public bool HasLostOwnership { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();

            try
            {
                await _renewal;
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                // Expected when the delivery path finishes before the next renewal.
            }
            finally
            {
                _cancellation.Dispose();
            }
        }

        private async Task RenewAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_owner._options.LeaseRenewalIntervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(_cancellation.Token))
                {
                    if (await _owner.RenewLeaseAsync(_message, _cancellation.Token))
                    {
                        continue;
                    }

                    HasLostOwnership = true;
                    await _cancellation.CancelAsync();
                    return;
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                // The active delivery completed or was canceled.
            }
            catch (Exception ex)
            {
                HasLostOwnership = true;
                _owner._logger.LogError(
                    ex,
                    "Stopped leave-request email delivery because its lease could not be renewed. OutboundMessageId={OutboundMessageId}",
                    _message.Id);
                await _cancellation.CancelAsync();
            }
        }
    }

    private sealed record ClaimedOutboundMessage(
        int Id,
        int LeaveRequestId,
        string NotificationType,
        string RecipientEmail,
        Guid LockId,
        int AttemptCount);

    private sealed record FailureResult(bool Updated, bool IsDeadLetter);

    private sealed record LeaveRequestEmailNotification(string Subject, string Header, string Message);
}
