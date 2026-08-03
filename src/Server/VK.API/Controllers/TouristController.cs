using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VK.API.Common;
using VK.API.Services.AppServices;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TouristController : ControllerBase
{
    private readonly ITouristAppService _touristAppService;

    public TouristController(ITouristAppService touristAppService)
    {
        _touristAppService = touristAppService;
    }

    /// <summary>
    /// Register or re-register a tourist by DeviceId.
    /// Returns a JWT bearer token in the response that must be used for all subsequent
    /// tourist-scoped requests (Authorization: Bearer {token}).
    /// This endpoint is intentionally anonymous — no token required to call it.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(TouristDto), 200)]
    public async Task<IActionResult> RegisterTourist([FromBody] RegisterTouristRequest request)
        => Ok(await _touristAppService.RegisterTouristAsync(request));

    /// <summary>Update the authenticated tourist's GPS location.</summary>
    [Authorize]
    [HttpPut("{touristId}/location")]
    [ProducesResponseType(typeof(UpdateLocationResultDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateLocation(int touristId, [FromBody] UpdateLocationRequest request)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return (await _touristAppService.UpdateLocationAsync(touristId, request)).ToActionResult();
    }

    /// <summary>Log a POI visit for the authenticated tourist.</summary>
    [Authorize]
    [HttpPost("{touristId}/visits")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> LogVisit(int touristId, [FromBody] LogVisitRequest request)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return (await _touristAppService.LogVisitAsync(touristId, request)).ToActionResult();
    }

    /// <summary>Get the authenticated tourist's visit history.</summary>
    [Authorize]
    [HttpGet("{touristId}/visits")]
    [ProducesResponseType(typeof(IReadOnlyList<VisitHistoryDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetVisitHistory(int touristId)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return Ok(await _touristAppService.GetVisitHistoryAsync(touristId));
    }

    /// <summary>Add a POI to the authenticated tourist's favourites.</summary>
    [Authorize]
    [HttpPost("{touristId}/favorites")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddFavorite(int touristId, [FromBody] AddFavoriteRequest request)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return (await _touristAppService.AddFavoriteAsync(touristId, request)).ToActionResult();
    }

    /// <summary>Remove a POI from the authenticated tourist's favourites.</summary>
    [Authorize]
    [HttpDelete("{touristId}/favorites/{poiId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> RemoveFavorite(int touristId, int poiId)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return (await _touristAppService.RemoveFavoriteAsync(touristId, poiId)).ToActionResult();
    }

    /// <summary>Get the authenticated tourist's favourite POIs.</summary>
    [Authorize]
    [HttpGet("{touristId}/favorites")]
    [ProducesResponseType(typeof(IReadOnlyList<POIListItemDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetFavorites(
        int touristId,
        [FromQuery] string languageCode = LanguageConstants.Vietnamese)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return Ok(await _touristAppService.GetFavoritesAsync(touristId, languageCode));
    }

    /// <summary>Submit a rating for a POI on behalf of the authenticated tourist.</summary>
    [Authorize]
    [HttpPost("{touristId}/ratings")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SubmitRating(int touristId, [FromBody] SubmitRatingRequest request)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        return (await _touristAppService.SubmitRatingAsync(touristId, request)).ToActionResult();
    }

    /// <summary>Get the authenticated tourist's activity statistics.</summary>
    [Authorize]
    [HttpGet("{touristId}/stats")]
    [ProducesResponseType(typeof(TouristStatsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStats(int touristId)
    {
        var ownership = VerifyOwnership(touristId);
        if (ownership != null) return ownership;
        var stats = await _touristAppService.GetStatsAsync(touristId);
        return stats is null
            ? NotFound(new { message = "Tourist không tồn tại" })
            : Ok(stats);
    }

    // ── IDOR guard ────────────────────────────────────────────────────────────
    /// <summary>
    /// Returns a 403 ForbidResult when the JWT subject does not match <paramref name="touristId"/>,
    /// unless the caller has the Admin role (admins can access any tourist's data).
    /// Returns null when access is permitted.
    /// </summary>
    private IActionResult? VerifyOwnership(int touristId)
    {
        // Admins may access any tourist profile
        if (User.IsInRole("Admin"))
            return null;

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub == null || !int.TryParse(sub, out var claimedId))
            return Unauthorized(new { message = "Invalid token: missing subject claim." });

        if (claimedId != touristId)
            return StatusCode(403, new
            {
                message = "Access denied: you can only access your own tourist profile.",
                yourTouristId = claimedId,
                requestedTouristId = touristId
            });

        return null;
    }
}
