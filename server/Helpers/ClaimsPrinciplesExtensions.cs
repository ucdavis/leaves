using System;
using System.Security.Claims;
using Microsoft.Identity.Web;

namespace Server.Helpers;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's Entra object ID or equivalent principal ID.
    /// Falls back to the legacy OID claim and name identifier if needed.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated principal is missing an object ID claim.");
        }

        return userId;
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out string userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var userIdValue =
            principal.FindFirstValue(ClaimConstants.Oid)
            ?? principal.FindFirstValue(ClaimConstants.ObjectId)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            userId = string.Empty;
            return false;
        }

        userId = userIdValue;
        return true;
    }
}
