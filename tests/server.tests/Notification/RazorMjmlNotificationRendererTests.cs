using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.Core.Notification;

namespace Server.Tests.Notification;

public class RazorMjmlNotificationRendererTests
{
    [Fact]
    public async Task RenderAsync_renders_html_from_a_server_core_template()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SmtpOptions.SectionName}:Host"] = "sandbox.smtp.mailtrap.io",
                [$"{SmtpOptions.SectionName}:Port"] = "587",
                [$"{SmtpOptions.SectionName}:Username"] = "smtp-user",
                [$"{SmtpOptions.SectionName}:Password"] = "smtp-password",
                [$"{SmtpOptions.SectionName}:FromEmail"] = "no-reply@example.test",
                [$"{SmtpOptions.SectionName}:FromName"] = "Template App",
            })
            .Build();

        services.AddLogging();
        services.AddNotificationServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var renderer = scope.ServiceProvider.GetRequiredService<INotificationRenderer>();

        var html = await renderer.RenderAsync("/Views/Emails/DefaultNotification_mjml.cshtml", new DefaultNotificationTemplateModel
        {
            AppName = "Template App",
            AppHomeUrl = "https://example.test",
            Header = "Render Test",
            LogoUrl = "https://example.test/apple-touch.png",
            Paragraphs =
            [
                "Request details\nName: Test Faculty\nEmail: faculty@example.test",
            ],
        });

        html.Should().Contain("Template App");
        html.Should().Contain("https://example.test/apple-touch.png");
        html.Should().Contain("Render Test");
        html.Should().Contain("Request details");
        html.Should().Contain("Name: Test Faculty");
        html.Should().Contain("Email: faculty@example.test");
        html.Should().NotContain("<mjml");
    }

    [Fact]
    public async Task RenderAsync_renders_html_from_the_table_template()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SmtpOptions.SectionName}:Host"] = "sandbox.smtp.mailtrap.io",
                [$"{SmtpOptions.SectionName}:Port"] = "587",
                [$"{SmtpOptions.SectionName}:Username"] = "smtp-user",
                [$"{SmtpOptions.SectionName}:Password"] = "smtp-password",
                [$"{SmtpOptions.SectionName}:FromEmail"] = "no-reply@example.test",
                [$"{SmtpOptions.SectionName}:FromName"] = "Template App",
            })
            .Build();

        services.AddLogging();
        services.AddNotificationServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var renderer = scope.ServiceProvider.GetRequiredService<INotificationRenderer>();

        var html = await renderer.RenderAsync("/Views/Emails/TableNotification_mjml.cshtml", new TableNotificationTemplateModel
        {
            AppName = "Template App",
            Header = "Statement",
            LayoutWidth = "640px",
            Message = "A dynamic table is rendered below.",
            Rows =
            [
                new NotificationTableRow
                {
                    Title = "Design",
                    Details = "Wireframes and feedback",
                    Amount = 90m,
                },
                new NotificationTableRow
                {
                    Title = "Build",
                    Details = "Frontend and backend implementation",
                    Amount = 240m,
                },
            ],
            TotalAmount = 330m,
        });

        html.Should().Contain("Statement");
        html.Should().Contain("A dynamic table is rendered below.");
        html.Should().Contain("Design");
        html.Should().Contain("Wireframes and feedback");
        html.Should().Contain("$330.00");
        html.Should().Contain("width:640px");
        html.Should().NotContain("border-collapse:collapse;width:100%;");
        html.Should().NotContain("<mjml");
    }
}
