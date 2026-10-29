
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public async Task SendVerificationEmailAsync(
        string recipientEmail,
        string recipientName,
        string verificationUrl,
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

        var safeName = System.Net.WebUtility.HtmlEncode(
            recipientName);

        var safeUrl = System.Net.WebUtility.HtmlEncode(
            verificationUrl);

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
            subject = "Verify your FootballSim account",
            htmlContent = $"""
                <!DOCTYPE html>
                <html lang="en">
                <body style="font-family:Arial,sans-serif;
                             background:#f4f6f8;
                             padding:30px;">
                    <div style="max-width:520px;
                                margin:auto;
                                background:white;
                                padding:32px;
                                border-radius:12px;">
                        <h1 style="color:#111827;">
                            Welcome to FootballSim!
                        </h1>

                        <p>Hello {safeName},</p>

                        <p>
                            Thank you for registering.
                            Please verify your email address
                            to activate your account.
                        </p>

                        <a href="{safeUrl}"
                           style="display:inline-block;
                                  padding:14px 24px;
                                  background:#111827;
                                  color:white;
                                  text-decoration:none;
                                  border-radius:8px;">
                            Verify Email Address
                        </a>

                        <p>
                            This verification link expires
                            after 24 hours.
                        </p>

                        <p>
                            If you did not create this account,
                            you can ignore this email.
                        </p>

                        <p>FootballSim Team</p>
                    </div>
                </body>
                </html>
                """,
            textContent =
                $"Hello {recipientName},\n\n" +
                "Verify your FootballSim account using this link:\n" +
                $"{verificationUrl}\n\n" +
                "This link expires after 24 hours."
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
                "Brevo email delivery request failed. HTTP {StatusCode}",
                (int)response.StatusCode);

            throw new HttpRequestException(
                "Unable to submit the verification email.");
        }

        _logger.LogInformation(
            "Brevo accepted a verification email request.");
    }
}
