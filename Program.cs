
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.Entities;
using ChampionsLeagueSimulatorAPI.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using System.Security.Claims;
using System.Text;

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

// Knockout services
builder.Services.AddScoped<KnockoutDrawService>();
builder.Services.AddScoped<KnockoutSimulationService>();
builder.Services.AddScoped<KnockoutBatchSimulationService>();

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
// SWAGGER CONFIGURATION
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // --------------------------------------
    // API DOCUMENTATION
    // --------------------------------------

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FootballSim API",
        Version = "v1",

        Description =
            "FootballSim - Champions League " +
            "Simulation and Competition Management API"
    });

    // --------------------------------------
    // JWT BEARER SECURITY DEFINITION
    // --------------------------------------

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
                "Enter your JWT access token to " +
                "authenticate protected API endpoints."
        });

    // --------------------------------------
    // APPLY JWT SECURITY REQUIREMENT
    // --------------------------------------

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,

                        Id = JwtBearerDefaults
                            .AuthenticationScheme
                    }
                },

                Array.Empty<string>()
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

    options.DocumentTitle = "FootballSim API Documentation";
});

// ==========================================
// HTTP MIDDLEWARE
// ==========================================

app.UseHttpsRedirection();

app.UseCors("Frontend");

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
