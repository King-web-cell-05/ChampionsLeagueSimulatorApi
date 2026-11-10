
using System.Security.Claims;

using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    private const long MaxLogoSize = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> ImageTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };

    public TeamsController(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // ==========================================
    // AUTHENTICATED USER ID
    // ==========================================

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    // ==========================================
    // SAFE TEAM RESPONSE
    // ==========================================

    private static object ToTeamResponse(Team team)
    {
        return new
        {
            team.Id,
            team.Name,
            team.Country,
            team.Coefficient,
            team.LogoUrl,
            team.IsDefault
        };
    }

    // ==========================================
    // GET VISIBLE TEAMS
    // GET: api/Teams
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var teams = await _context.Teams
            .AsNoTracking()
            .Where(t =>
                t.IsDefault ||
                t.OwnerId == userId.Value)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Country,
                t.Coefficient,
                t.LogoUrl,
                t.IsDefault
            })
            .ToListAsync();

        return Ok(teams);
    }

    // ==========================================
    // GET SINGLE VISIBLE TEAM
    // GET: api/Teams/{id}
    // ==========================================

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTeam(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var team = await _context.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.Id == id &&
                (t.IsDefault ||
                 t.OwnerId == userId.Value));

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        return Ok(ToTeamResponse(team));
    }

    // ==========================================
    // CREATE PRIVATE TEAM
    // POST: api/Teams
    // ==========================================

    [HttpPost]
    public async Task<IActionResult> CreateTeam(
        [FromBody] CreateTeamRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var validationError = ValidateTeamRequest(request);

        if (validationError is not null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        var name = request.Name.Trim();
        var country = request.Country.Trim();

        // A user cannot duplicate a shared default club
        // or another club belonging to their own account.
        // Other users' private club names are independent.
        var duplicateExists = await _context.Teams
            .AnyAsync(t =>
                (t.IsDefault ||
                 t.OwnerId == userId.Value) &&
                t.Name.ToLower() == name.ToLower());

        if (duplicateExists)
        {
            return Conflict(new
            {
                message =
                    "You already have a club with this name, " +
                    "or it is a shared default club."
            });
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Country = country,
            Coefficient = request.Coefficient,
            LogoUrl = NormalizeLogoUrl(request.LogoUrl),

            // Always create private clubs.
            IsDefault = false,

            // Ownership comes from the validated JWT.
            OwnerId = userId.Value
        };

        _context.Teams.Add(team);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTeam),
            new { id = team.Id },
            ToTeamResponse(team));
    }

    // ==========================================
    // UPDATE OWN PRIVATE TEAM
    // PUT: api/Teams/{id}
    // ==========================================

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTeam(
        Guid id,
        [FromBody] CreateTeamRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var validationError = ValidateTeamRequest(request);

        if (validationError is not null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        // Never load another user's private team
        // for modification.
        var team = await _context.Teams
            .FirstOrDefaultAsync(t =>
                t.Id == id &&
                !t.IsDefault &&
                t.OwnerId == userId.Value);

        if (team is null)
        {
            return NotFound(new
            {
                message =
                    "Editable team not found."
            });
        }

        var name = request.Name.Trim();
        var country = request.Country.Trim();

        var duplicateExists = await _context.Teams
            .AnyAsync(t =>
                t.Id != id &&
                (t.IsDefault ||
                 t.OwnerId == userId.Value) &&
                t.Name.ToLower() == name.ToLower());

        if (duplicateExists)
        {
            return Conflict(new
            {
                message =
                    "Another available club already " +
                    "uses this name."
            });
        }

        team.Name = name;
        team.Country = country;
        team.Coefficient = request.Coefficient;
        team.LogoUrl = NormalizeLogoUrl(request.LogoUrl);

        // Do not modify OwnerId or IsDefault.

        await _context.SaveChangesAsync();

        return Ok(ToTeamResponse(team));
    }

    // ==========================================
    // UPLOAD PRIVATE TEAM LOGO
    // POST: api/Teams/{id}/logo
    // ==========================================

    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxLogoSize + 64 * 1024)]
    public async Task<IActionResult> UploadTeamLogo(
        Guid id,
        IFormFile file)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        // Only the owner may upload a crest.
        // Shared default clubs are protected.
        var team = await _context.Teams
            .FirstOrDefaultAsync(t =>
                t.Id == id &&
                !t.IsDefault &&
                t.OwnerId == userId.Value);

        if (team is null)
        {
            return NotFound(new
            {
                message =
                    "Editable team not found."
            });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a logo image."
            });
        }

        if (file.Length > MaxLogoSize)
        {
            return BadRequest(new
            {
                message = "Logo must not exceed 2 MB."
            });
        }

        var extension = Path.GetExtension(file.FileName);

        if (!ImageTypes.TryGetValue(
                extension,
                out var expectedContentType))
        {
            return BadRequest(new
            {
                message =
                    "Only PNG, JPG, and WebP images are supported."
            });
        }

        if (!string.Equals(
                file.ContentType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Image file type does not match its extension."
            });
        }

        // Verify the image signature.
        await using (var stream = file.OpenReadStream())
        {
            var header = new byte[12];

            var bytesRead = await stream.ReadAsync(
                header.AsMemory(0, header.Length));

            if (!HasValidImageSignature(
                    header,
                    bytesRead,
                    extension))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid or unsupported image file."
                });
            }
        }

        var webRoot = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");
        }

        var uploadDirectory = Path.Combine(
            webRoot,
            "uploads",
            "team-logos");

        Directory.CreateDirectory(uploadDirectory);

        var filename =
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var filePath = Path.Combine(
            uploadDirectory,
            filename);

        await using (var output = new FileStream(
            filePath,
            FileMode.CreateNew))
        {
            await file.CopyToAsync(output);
        }

        var logoPath =
            $"/uploads/team-logos/{filename}";

        var oldLogoUrl = team.LogoUrl;

        team.LogoUrl = logoPath;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            throw;
        }

        // Remove an old local crest only after
        // the new logo has been saved successfully.
        if (!string.IsNullOrWhiteSpace(oldLogoUrl) &&
            oldLogoUrl.StartsWith(
                "/uploads/team-logos/",
                StringComparison.Ordinal))
        {
            var oldFilename =
                Path.GetFileName(oldLogoUrl);

            var oldPath = Path.Combine(
                uploadDirectory,
                oldFilename);

            if (System.IO.File.Exists(oldPath) &&
                !string.Equals(
                    oldPath,
                    filePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    System.IO.File.Delete(oldPath);
                }
                catch (IOException)
                {
                    // Cleanup can happen later.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cleanup can happen later.
                }
            }
        }

        return Ok(new
        {
            message =
                "Team logo uploaded successfully.",
            teamId = team.Id,
            logoUrl = logoPath
        });
    }

    // ==========================================
    // VALIDATE TEAM INPUT
    // ==========================================

    private static string? ValidateTeamRequest(
        CreateTeamRequest request)
    {
        if (request is null)
            return "Team information is required.";

        if (string.IsNullOrWhiteSpace(request.Name))
            return "Team name is required.";

        if (string.IsNullOrWhiteSpace(request.Country))
            return "Country is required.";

        if (request.Name.Trim().Length > 150)
            return "Team name must not exceed 150 characters.";

        if (request.Country.Trim().Length > 100)
            return "Country must not exceed 100 characters.";

        if (request.Coefficient < 0 ||
            request.Coefficient > 200)
        {
            return "Coefficient must be between 0 and 200.";
        }

        if (!IsValidLogoUrl(request.LogoUrl))
        {
            return
                "Logo URL must be a valid HTTPS URL " +
                "or a supported local logo path.";
        }

        return null;
    }

    // ==========================================
    // IMAGE SIGNATURE VALIDATION
    // ==========================================

    private static bool HasValidImageSignature(
        byte[] header,
        int bytesRead,
        string extension)
    {
        extension = extension.ToLowerInvariant();

        if (extension == ".png")
        {
            byte[] signature =
            {
                137, 80, 78, 71, 13, 10, 26, 10
            };

            return bytesRead >= 8 &&
                header.Take(8).SequenceEqual(signature);
        }

        if (extension is ".jpg" or ".jpeg")
        {
            return bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;
        }

        if (extension == ".webp")
        {
            return bytesRead >= 12 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P';
        }

        return false;
    }

    // ==========================================
    // LOGO URL VALIDATION
    // ==========================================

    private static bool IsValidLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return true;

        var value = logoUrl.Trim();

        if (value.StartsWith(
                "/uploads/team-logos/",
                StringComparison.Ordinal))
        {
            var filename = value[
                "/uploads/team-logos/".Length..];

            return !string.IsNullOrWhiteSpace(filename) &&
                   filename == Path.GetFileName(filename) &&
                   !filename.Contains('\\') &&
                   ImageTypes.ContainsKey(
                       Path.GetExtension(filename));
        }

        return Uri.TryCreate(
            value,
            UriKind.Absolute,
            out var uri
        ) && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string? NormalizeLogoUrl(
        string? logoUrl)
    {
        return string.IsNullOrWhiteSpace(logoUrl)
            ? null
            : logoUrl.Trim();
    }
}
