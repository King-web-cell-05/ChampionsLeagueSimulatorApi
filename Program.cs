
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

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

var jwtKeyBytes = Convert.FromBase64String(jwtKeySetting);

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "The JWT signing key must contain at least 32 bytes.");
}

// ==========================================
// CONTROLLERS
// ==========================================

builder.Services.AddControllers();

// ==========================================
// APPLICATION SERVICES
// ==========================================

builder.Services.AddScoped<TableService>();
builder.Services.AddScoped<DrawService>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddScoped<CompetitionService>();

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
    options.UseNpgsql(connectionString));

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
                ClockSkew = TimeSpan.FromMinutes(1),

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
    });

// ==========================================
// AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// CORS CONFIGURATION
// ==========================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
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

// Uses the direct client IP.
// Configure trusted forwarded headers separately
// if deploying behind a reverse proxy.
static string GetClientPartition(HttpContext context)
{
    return context.Connection.RemoteIpAddress?
        .ToString() ?? "unknown";
}

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

    // ======================================
    // JWT BEARER SECURITY DEFINITION
    // ======================================

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

    // ======================================
    // JWT SECURITY REQUIREMENT
    // Swashbuckle 10.1.3
    // Microsoft.OpenApi 2.7.5
    // ======================================

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
// BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// SWAGGER MIDDLEWARE
// ==========================================

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

// ==========================================
// HTTPS REDIRECTION
// ==========================================

app.UseHttpsRedirection();

// ==========================================
// CORS
// ==========================================

app.UseCors("Frontend");

// ==========================================
// RATE LIMITING
// ==========================================

app.UseRouting();

app.UseRateLimiter();

// ==========================================
// AUTHENTICATION & AUTHORIZATION
// ==========================================

app.UseAuthentication();

app.UseAuthorization();

// ==========================================
// MAP CONTROLLERS
// ==========================================

app.MapControllers();

// ==========================================
// RUN APPLICATION
// ==========================================

app.Run();
