using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Server.Core.Notification;

public interface INotificationService
{
    Task SendLeaveRequestAsync(
        EmailRecipients recipients,
        string subject,
        LeaveRequestEmailContent content,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationService : INotificationService
{
    private const string LeaveRequestTemplatePath = "/Views/Emails/LeaveRequestNotification_mjml.cshtml";

    private readonly IEmailService _emailService;
    private readonly NotificationOptions _notificationOptions;
    private readonly INotificationRenderer _notificationRenderer;
    private readonly SmtpOptions _smtpOptions;

    public NotificationService(
        IEmailService emailService,
        INotificationRenderer notificationRenderer,
        IOptions<NotificationOptions> notificationOptions,
        IOptions<SmtpOptions> smtpOptions)
    {
        _emailService = emailService;
        _notificationRenderer = notificationRenderer;
        _notificationOptions = notificationOptions.Value;
        _smtpOptions = smtpOptions.Value;
    }

    public async Task SendLeaveRequestAsync(
        EmailRecipients recipients,
        string subject,
        LeaveRequestEmailContent content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ValidationException("Notification subject is required.");
        }

        EmailValidation.ValidateRecipients(recipients);

        var appName = string.IsNullOrWhiteSpace(_notificationOptions.DefaultAppName)
            ? _smtpOptions.FromName
            : _notificationOptions.DefaultAppName;
        var appBaseUri = TryGetAppBaseUri(_notificationOptions.BaseUrl);
        var model = new LeaveRequestNotificationTemplateModel
        {
            AppName = appName,
            AppHomeUrl = appBaseUri?.ToString().TrimEnd('/'),
            Content = content,
            Header = content.Header,
            LogoUrl = BuildAppAssetUrl(appBaseUri, "/apple-touch.png") ?? NotificationTemplateModelBase.DefaultLogoUrl,
        };

        var htmlBody = await _notificationRenderer.RenderAsync(
            LeaveRequestTemplatePath,
            model,
            cancellationToken);

        await _emailService.SendAsync(new EmailMessage
        {
            Recipients = recipients,
            Subject = subject,
            TextBody = content.ToTextBody(),
            HtmlBody = htmlBody,
        }, cancellationToken);
    }

    private static Uri? TryGetAppBaseUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri;
    }

    private static string? BuildAppAssetUrl(Uri? appBaseUri, string assetPath)
    {
        if (appBaseUri is null)
        {
            return null;
        }

        var builder = new UriBuilder(appBaseUri)
        {
            Path = $"{appBaseUri.AbsolutePath.TrimEnd('/')}/{assetPath.TrimStart('/')}",
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return builder.Uri.ToString();
    }

}
