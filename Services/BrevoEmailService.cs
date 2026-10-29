
using System.Net;
using System.Net.Http.Json;

namespace ChampionsLeagueSimulatorAPI.Services;

public sealed class BrevoEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendVerificationCodeAsync(
        string recipientEmail,
        string recipientName,
        string code,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Brevo:ApiKey"];
        var senderEmail = _configuration["Brevo:SenderEmail"];
        var senderName = _configuration["Brevo:SenderName"]
            ?? "FootballSim";

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Brevo API key is not configured.");

        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException(
                "Brevo sender email is not configured.");

        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeCode = WebUtility.HtmlEncode(code);

        var payload = new
        {
            sender = new
            {
                name = senderName,
                email = senderEmail
            },
            to = new[]
            {
                new
                {
                    email = recipientEmail,
                    name = recipientName
                }
            },
            subject = "Your FootballSim verification code",
            htmlContent = $"""
                <!DOCTYPE html>
                <html lang="en">
                <body style="margin:0;padding:32px 16px;
                             background:#f3f4f6;
                             font-family:Arial,sans-serif;">
                    <div style="max-width:520px;margin:auto;
                                padding:36px 28px;
                                background:#ffffff;
                                border-radius:16px;">
                        <h1 style="color:#111827;">
                            FootballSim
                        </h1>

                        <h2 style="color:#111827;">
                            Verify your email address
                        </h2>

                        <p>Hello {safeName},</p>

                        <p>
                            Enter this code to verify your
                            FootballSim account:
                        </p>

                        <div style="padding:22px;
                                    margin:24px 0;
                                    text-align:center;
                                    background:#f3f4f6;
                                    border-radius:12px;">
                            <span style="font-size:36px;
                                         font-weight:700;
                                         letter-spacing:8px;
                                         color:#111827;">
                                {safeCode}
                            </span>
                        </div>

                        <p>
                            This code expires in
                            <strong>10 minutes</strong>.
                        </p>

                        <p>
                            If you didn't register for
                            FootballSim, ignore this email.
                        </p>

                        <p style="color:#6b7280;">
                            FootballSim Team
                        </p>
                    </div>
                </body>
                </html>
                """,
            textContent =
                $"Hello {recipientName},\n\n" +
                $"Your FootballSim verification code is: {code}\n\n" +
                "This code expires in 10 minutes.\n\n" +
                "If you didn't register, ignore this email."
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.brevo.com/v3/smtp/email");

        request.Headers.Add("api-key", apiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Brevo rejected verification email. HTTP {StatusCode}",
                (int)response.StatusCode);

            throw new HttpRequestException(
                "Unable to submit verification email.");
        }

        _logger.LogInformation(
            "Brevo accepted verification email request.");
    }
}
