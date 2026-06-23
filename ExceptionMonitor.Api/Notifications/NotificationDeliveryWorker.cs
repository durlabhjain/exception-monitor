using Dapper;
using ExceptionMonitor.Api.Database;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ExceptionMonitor.Api.Notifications;

public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification worker failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, notificationOptions.Value.WorkerIntervalSeconds)), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = await db.OpenConnectionAsync(cancellationToken);
        var deliveries = (await connection.QueryAsync<PendingDelivery>(new CommandDefinition(
            @"select d.id, d.delivery_type as DeliveryType, d.subject, d.body, d.attempt_count as AttemptCount,
                     r.email, r.webhook_url as WebhookUrl
              from notification_deliveries d
              inner join notification_recipients r on r.id = d.recipient_id
              where d.status = 'Pending' and d.next_attempt_at <= now()
              order by d.created_at
              limit 25",
            cancellationToken: cancellationToken))).ToList();

        foreach (var delivery in deliveries)
        {
            try
            {
                if (delivery.DeliveryType == "Email")
                {
                    await SendEmailAsync(delivery, cancellationToken);
                }
                else if (delivery.DeliveryType == "Webhook")
                {
                    await SendWebhookAsync(delivery, cancellationToken);
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "update notification_deliveries set status = 'Sent', sent_at = now(), last_attempt_at = now(), attempt_count = attempt_count + 1 where id = @Id",
                    new { delivery.Id }, cancellationToken: cancellationToken));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification delivery {DeliveryId} failed", delivery.Id);
                var abandoned = delivery.AttemptCount >= 4;
                await connection.ExecuteAsync(new CommandDefinition(
                    @"update notification_deliveries
                      set status = case when @Abandoned then 'Abandoned' else 'Pending' end,
                          last_attempt_at = now(),
                          attempt_count = attempt_count + 1,
                          last_error = @Error,
                          next_attempt_at = now() + make_interval(mins => least(60, power(2, attempt_count)::int))
                      where id = @Id",
                    new { delivery.Id, Abandoned = abandoned, Error = ex.Message }, cancellationToken: cancellationToken));
            }
        }
    }

    private async Task SendEmailAsync(PendingDelivery delivery, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(delivery.Email))
        {
            throw new InvalidOperationException("Email recipient is missing.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(notificationOptions.Value.FromName, notificationOptions.Value.FromEmail));
        message.To.Add(MailboxAddress.Parse(delivery.Email));
        message.Subject = delivery.Subject;
        message.Body = new TextPart("plain") { Text = delivery.Body };

        using var client = new SmtpClient();
        var socketOptions = smtpOptions.Value.UseSsl
            ? (smtpOptions.Value.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
            : SecureSocketOptions.None;
        await client.ConnectAsync(smtpOptions.Value.Host, smtpOptions.Value.Port, socketOptions, cancellationToken);
        if (!string.IsNullOrWhiteSpace(smtpOptions.Value.Username))
        {
            await client.AuthenticateAsync(smtpOptions.Value.Username, smtpOptions.Value.Password ?? string.Empty, cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static async Task SendWebhookAsync(PendingDelivery delivery, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(delivery.WebhookUrl))
        {
            throw new InvalidOperationException("Webhook URL is missing.");
        }

        using var client = new HttpClient();
        using var response = await client.PostAsJsonAsync(delivery.WebhookUrl, new { delivery.Subject, delivery.Body, DeliveryId = delivery.Id }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record PendingDelivery(Guid Id, string DeliveryType, string Subject, string Body, int AttemptCount, string? Email, string? WebhookUrl);
}
