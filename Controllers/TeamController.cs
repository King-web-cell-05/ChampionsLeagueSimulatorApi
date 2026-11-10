
using ChampionsLeagueSimulatorAPI.Data;
using ChampionsLeagueSimulatorAPI.DTOs;
using ChampionsLeagueSimulatorAPI.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChampionsLeagueSimulatorAPI.Controllers;

[ApiController]
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
    // GET ALL TEAMS
    // GET: api/Teams
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _context.Teams
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(teams);
    }

    // ==========================================
    // GET SINGLE TEAM
    // GET: api/Teams/{id}
    // ==========================================

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTeam(Guid id)
    {
        var team = await _context.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        return Ok(team);
    }

    // ==========================================
    // CREATE TEAM
    // POST: api/Teams
    // ==========================================

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateTeam(
        [FromBody] CreateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Country))
        {
            return BadRequest(new
            {
                message = "Team name and country are required."
            });
        }

        if (!IsValidLogoUrl(request.LogoUrl))
        {
            return BadRequest(new
            {
                message = "Logo URL must be a valid HTTPS URL."
            });
        }

        var name = request.Name.Trim();
        var country = request.Country.Trim();

        var duplicateExists = await _context.Teams
            .AnyAsync(t =>
                t.Name.ToLower() == name.ToLower());

        if (duplicateExists)
        {
            return Conflict(new
            {
                message = "A team with this name already exists."
            });
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Country = country,
            Coefficient = request.Coefficient,
            LogoUrl = NormalizeLogoUrl(request.LogoUrl)
        };

        _context.Teams.Add(team);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTeam),
            new { id = team.Id },
            team
        );
    }

    // ==========================================
    // UPDATE TEAM
    // PUT: api/Teams/{id}
    // ==========================================

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTeam(
        Guid id,
        [FromBody] CreateTeamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Country))
        {
            return BadRequest(new
            {
                message = "Team name and country are required."
            });
        }

        if (!IsValidLogoUrl(request.LogoUrl))
        {
            return BadRequest(new
            {
                message = "Logo URL must be a valid HTTPS URL."
            });
        }

        var team = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
            });
        }

        var name = request.Name.Trim();
        var country = request.Country.Trim();

        var duplicateExists = await _context.Teams
            .AnyAsync(t =>
                t.Id != id &&
                t.Name.ToLower() == name.ToLower());

        if (duplicateExists)
        {
            return Conflict(new
            {
                message = "Another team already has this name."
            });
        }

        team.Name = name;
        team.Country = country;
        team.Coefficient = request.Coefficient;
        team.LogoUrl = NormalizeLogoUrl(request.LogoUrl);

        await _context.SaveChangesAsync();

        return Ok(team);
    }

    // ==========================================
    // UPLOAD TEAM LOGO
    // POST: api/Teams/{id}/logo
    // ==========================================


    [Authorize]
    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxLogoSize + 64 * 1024)]
    public async Task<IActionResult> UploadTeamLogo(
        Guid id,
        IFormFile file)

    {
        var team = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team is null)
        {
            return NotFound(new
            {
                message = "Team not found."
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
                message = "Only PNG, JPG, and WebP images are supported."
            });
        }

        if (!string.Equals(
                file.ContentType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Image file type does not match its extension."
            });
        }

        // Verify image signature, not just extension/MIME.
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
                    message = "Invalid or unsupported image file."
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
            System.IO.File.Delete(filePath);
            throw;
        }

        // Remove the previous locally uploaded logo.
        // Never delete an external URL.
        if (!string.IsNullOrWhiteSpace(oldLogoUrl) &&
            oldLogoUrl.StartsWith(
                "/uploads/team-logos/",
                StringComparison.Ordinal))
        {
            var oldFilename = Path.GetFileName(oldLogoUrl);

            var oldPath = Path.Combine(
                uploadDirectory,
                oldFilename);

            if (System.IO.File.Exists(oldPath))
            {
                try
                {
                    System.IO.File.Delete(oldPath);
                }
                catch (IOException)
                {
                    // Old file can be cleaned up later.
                }
                catch (UnauthorizedAccessException)
                {
                    // Old file can be cleaned up later.
                }
            }
        }

        return Ok(new
        {
            message = "Team logo uploaded successfully.",
            teamId = team.Id,
            logoUrl = logoPath
        });
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
        {
            return true;
        }

        // Allow our own uploaded logo paths.
        if (logoUrl.StartsWith(
                "/uploads/team-logos/",
                StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(
            logoUrl.Trim(),
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
