namespace Server.Core.Notification;

public abstract class NotificationTemplateModelBase
{
    public static string DefaultLogoUrl => "/apple-touch.png";

    public string AppName { get; init; } = string.Empty;
    public string? AppHomeUrl { get; init; }
    public string? LayoutWidth { get; init; }
    public string? LogoUrl { get; init; } = DefaultLogoUrl;
}

public sealed class LeaveRequestNotificationTemplateModel : NotificationTemplateModelBase
{
    public string Header { get; init; } = string.Empty;
    public required LeaveRequestEmailContent Content { get; init; }
}

public sealed record LeaveRequestEmailContent(
    string Header,
    string RequesterName,
    string RequesterEmail,
    string Department,
    string LeaveType,
    string? PayLeaveType,
    string Dates,
    string TotalHours,
    string Note,
    string Status,
    string DecidedBy,
    string DecisionComment,
    string RequestId,
    bool IsApproved)
{
    public string ToTextBody()
    {
        var lines = new List<string>
        {
            Header,
            string.Empty,
            $"Name: {RequesterName}",
            $"Email: {RequesterEmail}",
            $"Department: {Department}",
            $"Leave type: {LeaveType}",
        };

        if (!string.IsNullOrWhiteSpace(PayLeaveType))
        {
            lines.Add($"FMLA pay type: {PayLeaveType}");
        }

        lines.AddRange(
        [
            $"Date(s): {Dates}",
            $"Total hours: {TotalHours}",
            $"Note: {Note}",
        ]);

        if (IsApproved)
        {
            lines.Add($"Approved by: {DecidedBy}");
            lines.Add($"Approval comment: {DecisionComment}");
        }
        else
        {
            lines.Add($"Status: {Status}");
            lines.Add($"Decided by: {DecidedBy}");
            lines.Add($"Comment: {DecisionComment}");
        }

        lines.Add($"Request ID: {RequestId}");
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record LeaveRequestDetailRow(string Label, string Value, bool IsFinalRow = false);
