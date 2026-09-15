using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Server.Core.Notification;

namespace Server.Services;

/// <summary>
/// Sends new leave-request notifications as soon as their committed transaction signals the worker.
/// The periodic recovery pass handles messages that existed before a restart or need a retry.
/// </summary>
public sealed class LeaveRequestEmailDeliveryBackgroundService : BackgroundService
{
    private readonly IEmailDeliveryWakeSignal _wakeSignal;
    private readonly EmailDeliveryOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaveRequestEmailDeliveryBackgroundService> _logger;

    public LeaveRequestEmailDeliveryBackgroundService(
        IEmailDeliveryWakeSignal wakeSignal,
        IOptions<EmailDeliveryOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<LeaveRequestEmailDeliveryBackgroundService> logger)
    {
        _wakeSignal = wakeSignal;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Leave-request email delivery is disabled.");
            return;
        }

        if (_options.RecoveryIntervalSeconds <= 0 || _options.MaxAttempts <= 0)
        {
            throw new InvalidOperationException(
                "EmailDelivery:RecoveryIntervalSeconds and EmailDelivery:MaxAttempts must be greater than zero.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDueAsync(stoppingToken);

            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var signaled = _wakeSignal.WaitAsync(waitCancellation.Token).AsTask();
            var recovery = Task.Delay(TimeSpan.FromSeconds(_options.RecoveryIntervalSeconds), waitCancellation.Token);
            await Task.WhenAny(signaled, recovery);
            await waitCancellation.CancelAsync();
            try
            {
                await Task.WhenAll(signaled, recovery);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Cancel the losing wait so it cannot consume a later wake signal.
            }
        }
    }

    private async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var delivery = scope.ServiceProvider.GetRequiredService<ILeaveRequestEmailDeliveryService>();
            var result = await delivery.ProcessDueAsync(DateTime.UtcNow, cancellationToken);

            if (result.ClaimedCount > 0)
            {
                _logger.LogInformation(
                    "Processed leave-request email batch. Claimed={ClaimedCount} Sent={SentCount} Retry={RetryCount} DeadLetter={DeadLetterCount}",
                    result.ClaimedCount,
                    result.SentCount,
                    result.RetryCount,
                    result.DeadLetterCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leave-request email delivery batch failed.");
        }
    }
}
