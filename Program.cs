
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// DATABASE CONNECTION
// ==========================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "The DefaultConnection connection string is missing.");

// ==========================================
// JWT CONFIGURATION
// ==========================================

var jwtKeySetting = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Jwt:Issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Jwt:Audience is missing.");

byte[] jwtKeyBytes;

try
{
    jwtKeyBytes = Convert.FromBase64String(jwtKeySetting);
}
catch (FormatException)
{
    throw new InvalidOperationException(
        "Jwt:Key must be a valid Base64-encoded value.");
}

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "The JWT signing key must contain at least 32 bytes.");
}

// ==========================================
// CONTROLLERS
// ==========================================

builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();

// ==========================================
// APPLICATION SERVICES
// ==========================================

builder.Services.AddScoped<TableService>();
builder.Services.AddScoped<DrawService>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<MatchEngineService>();

// ==========================================
// KNOCKOUT SERVICES
// ==========================================

builder.Services.AddScoped<KnockoutDrawService>();
builder.Services.AddScoped<KnockoutSimulationService>();
builder.Services.AddScoped<KnockoutBatchSimulationService>();

// ==========================================
// BREVO TRANSACTIONAL EMAIL
// ==========================================

builder.Services.AddHttpClient<BrevoEmailService>();

// ==========================================
// POSTGRESQL DATABASE
// ==========================================

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(
                typeof(AppDbContext).Assembly.GetName().Name);
        });
});

// ==========================================
// PASSWORD HASHING
// ==========================================

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

// ==========================================
// JWT AUTHENTICATION
// ==========================================

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        options.SaveToken = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtKeyBytes),

                ValidateLifetime = true,

                RequireExpirationTime = true,
                RequireSignedTokens = true,

                ClockSkew = TimeSpan.FromMinutes(1),

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

        // ======================================
        // DATABASE-BACKED JWT VALIDATION
        // ======================================

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                var tokenRole = context.Principal?
                    .FindFirstValue(ClaimTypes.Role);

                var tokenVersionValue = context.Principal?
                    .FindFirstValue("security_version");

                // Validate required JWT claims.
                if (!Guid.TryParse(
                        userIdValue,
                        out var userId) ||
                    string.IsNullOrWhiteSpace(tokenRole) ||
                    !int.TryParse(
                        tokenVersionValue,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var tokenVersion) ||
                    tokenVersion < 1)
                {
                    context.Fail("Invalid account claims.");
                    return;
                }

                var db = context.HttpContext.RequestServices
                    .GetRequiredService<AppDbContext>();

                // Retrieve the current account state.
                var account = await db.Users
                    .AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => new
                    {
                        user.Role,
                        user.IsSuspended,
                        user.IsEmailVerified,
                        user.SecurityVersion
                    })
                    .SingleOrDefaultAsync(
                        context.HttpContext.RequestAborted);

                // Reject deleted, suspended, unverified,
                // role-changed or revoked accounts.
                if (account is null ||
                    account.IsSuspended ||
                    !account.IsEmailVerified ||
                    !string.Equals(
                        account.Role,
                        tokenRole,
                        StringComparison.Ordinal) ||
                    account.SecurityVersion != tokenVersion)
                {
                    context.Fail(
                        "Account access has changed or session was revoked.");
                }
            }
        };
    });

// ==========================================
// AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// CORS CONFIGURATION
// ==========================================

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must contain at least one origin.");
}

foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(
            origin,
            UriKind.Absolute,
            out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp &&
         uri.Scheme != Uri.UriSchemeHttps) ||
        uri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        origin.EndsWith("/"))
    {
        throw new InvalidOperationException(
            $"Invalid CORS origin: {origin}");
    }

    if (!builder.Environment.IsDevelopment() &&
        uri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException(
            "Production CORS origins must use HTTPS.");
    }
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ==========================================
// AUTHENTICATION RATE LIMITING
// ==========================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // LOGIN: 5 requests per minute
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartition(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // REGISTER: 3 requests per 10 minutes
    options.AddPolicy("auth-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartition(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // VERIFY EMAIL: 10 requests per 10 minutes
    options.AddPolicy("auth-verify", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartition(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // RESEND CODE: 3 requests per 10 minutes
    options.AddPolicy("auth-resend", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartition(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ==========================================
// CLIENT RATE LIMIT PARTITION
// ==========================================

// Uses the direct connection IP.
// Configure trusted forwarded headers separately
// for your production reverse proxy.

static string GetClientPartition(HttpContext context)
{
    return context.Connection.RemoteIpAddress?
        .ToString() ?? "unknown";
}

// ==========================================
// HEALTH CHECKS
// ==========================================

builder.Services.AddHealthChecks();

// ==========================================
// SWAGGER CONFIGURATION
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FootballSim API",

        Version = "v1",

        Description =
            "FootballSim - UEFA Champions League " +
            "Simulation and Competition Management API. " +
            "Supports teams, competitions, league-phase " +
            "fixtures, standings, knockout rounds, " +
            "match simulation, and championship results."
    });

    // JWT BEARER SECURITY DEFINITION
    options.AddSecurityDefinition(
        JwtBearerDefaults.AuthenticationScheme,
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,

            Description =
                "Enter your JWT access token. " +
                "Swagger will automatically add " +
                "the Bearer prefix to requests."
        });

    // JWT SECURITY REQUIREMENT
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(
                    JwtBearerDefaults.AuthenticationScheme,
                    document
                ),

                new List<string>()
            }
        });
});

// ==========================================
// TEAM LOGO UPLOAD DIRECTORY
// ==========================================

var webRootPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "wwwroot");

Directory.CreateDirectory(webRootPath);

Directory.CreateDirectory(
    Path.Combine(
        webRootPath,
        "uploads",
        "team-logos"));

// ==========================================
// BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// GLOBAL EXCEPTION HANDLING
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType =
                "application/problem+json";

            await context.Response.WriteAsJsonAsync(
                new
                {
                    title = "Internal Server Error",
                    status = 500,
                    detail = "An unexpected error occurred."
                });
        });
    });

    app.UseHsts();
}

// ==========================================
// SECURITY HEADERS
// ==========================================

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] =
            "nosniff";

        context.Response.Headers["X-Frame-Options"] =
            "DENY";

        context.Response.Headers["Referrer-Policy"] =
            "strict-origin-when-cross-origin";

        return Task.CompletedTask;
    });

    await next(context);
});

// ==========================================
// SWAGGER - DEVELOPMENT ONLY
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "FootballSim API v1");

        options.DocumentTitle =
            "FootballSim API Documentation";

        options.DisplayRequestDuration();
        options.EnableDeepLinking();
    });
}

// ==========================================
// HTTPS REDIRECTION
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ==========================================
// STATIC FILES
// ==========================================

app.UseStaticFiles();

// ==========================================
// ROUTING
// ==========================================

app.UseRouting();

// ==========================================
// CORS
// ==========================================

app.UseCors("Frontend");

// ==========================================
// RATE LIMITING
// ==========================================

app.UseRateLimiter();

// ==========================================
// AUTHENTICATION
// ==========================================

app.UseAuthentication();

// ==========================================
// AUTHORIZATION
// ==========================================

app.UseAuthorization();

// ==========================================
// HEALTH CHECK ENDPOINT
// ==========================================

app.MapHealthChecks("/health");

// ==========================================
// API CONTROLLERS
// ==========================================

app.MapControllers();

// ==========================================
// RUN APPLICATION
// ==========================================

app.Run();
