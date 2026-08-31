using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Server.Core.Data;
using Server.Core.Domain;
using Server.Helpers;

namespace Server.Services;

public interface IUserService
{
    Task EnsureUserProfileAsync(
        ClaimsPrincipal principal,
        bool recordSignIn = true,
        CancellationToken cancellationToken = default);

    Task<string?> GetDisplayNameForUser(string userId);

    Task<List<string>> GetRolesForUser(string userId);

    Task<ClaimsPrincipal?> UpdateUserPrincipalIfNeeded(ClaimsPrincipal principal);
}

public class UserService : IUserService
{
    private const string AdminRole = "Admin";

    private readonly ILogger<UserService> _logger;
    private readonly AppDbContext _dbContext;

    public UserService(ILogger<UserService> logger, AppDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task EnsureUserProfileAsync(
        ClaimsPrincipal principal,
        bool recordSignIn = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.HasClaim("dev_persona", "true"))
        {
            return;
        }

        if (!principal.TryGetUserId(out var userId) || !Guid.TryParse(userId, out var entraObjectId))
        {
            _logger.LogWarning("Skipping user provisioning because the authenticated principal does not have a GUID object ID.");
            return;
        }

        var email =
            principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        var displayName =
            principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.Identity?.Name;

        var existingUser = await _dbContext.AppUsers
            .SingleOrDefaultAsync(appUser => appUser.EntraObjectId == entraObjectId, cancellationToken);
        var matchedPerson = await FindPersonByEmailAsync(email, cancellationToken);

        var iamId =
            NormalizeIamIdToken(matchedPerson?.IamId)
            ?? ResolveIamIdFromClaims(principal)
            ?? existingUser?.IamId;
        if (string.IsNullOrWhiteSpace(iamId))
        {
            throw new InvalidOperationException("IAM ID is required but was not found in the principal, matching person, or existing user record.");
        }
        var matchedPersonByIamId = await FindPersonByIamIdAsync(iamId, cancellationToken);
        var resolvedEmployeeId =
            NormalizeEmployeeId(matchedPerson?.EmployeeId)
            ?? NormalizeEmployeeId(matchedPersonByIamId?.EmployeeId)
            ?? existingUser?.EmployeeId;
        var resolvedDisplayName = !string.IsNullOrWhiteSpace(matchedPerson?.FullName)
            ? matchedPerson.FullName
            : !string.IsNullOrWhiteSpace(matchedPersonByIamId?.FullName)
                ? matchedPersonByIamId.FullName
            : displayName;

        var now = DateTime.UtcNow;
        var isNewUser = existingUser == null;
        var shouldSaveUser = false;
        if (isNewUser)
        {
            existingUser = new AppUser
            {
                DisplayName = resolvedDisplayName,
                Email = email ?? matchedPerson?.Email,
                EmployeeId = resolvedEmployeeId,
                EntraObjectId = entraObjectId,
                FirstLoginUtc = now,
                IamId = iamId,
                LastLoginUtc = now,
                UpdatedUtc = now
            };

            _dbContext.AppUsers.Add(existingUser);
            shouldSaveUser = true;
        }
        else
        {
            shouldSaveUser = ApplyExistingUserUpdates(
                existingUser!,
                resolvedDisplayName,
                email ?? matchedPerson?.Email,
                iamId,
                resolvedEmployeeId,
                recordSignIn,
                now);
        }

        if (shouldSaveUser)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (isNewUser && IsConcurrentInsert(ex))
            {
                _logger.LogDebug(
                    "User {EntraObjectId} was created concurrently; reloading and applying updates.",
                    entraObjectId);

                _dbContext.Entry(existingUser!).State = EntityState.Detached;

                var concurrentUser = await _dbContext.AppUsers
                    .SingleAsync(appUser => appUser.EntraObjectId == entraObjectId, cancellationToken);

                if (ApplyExistingUserUpdates(
                    concurrentUser,
                    resolvedDisplayName,
                    email ?? matchedPerson?.Email,
                    iamId,
                    resolvedEmployeeId,
                    recordSignIn,
                    now))
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    public async Task<string?> GetDisplayNameForUser(string userId)
    {
        if (!Guid.TryParse(userId, out var entraObjectId))
        {
            return null;
        }

        return await _dbContext.AppUsers
            .AsNoTracking()
            .Where(appUser => appUser.EntraObjectId == entraObjectId)
            .Select(appUser => appUser.DisplayName)
            .SingleOrDefaultAsync();
    }

    public async Task<List<string>> GetRolesForUser(string userId)
    {
        var iamId = await ResolveIamIdAsync(userId);
        if (string.IsNullOrWhiteSpace(iamId))
        {
            _logger.LogDebug("Could not resolve an IAM ID for user {UserId}; no app roles will be added.", userId);
            return [];
        }

        var isAdmin = await _dbContext.AppAdminAssignments
            .AsNoTracking()
            .AnyAsync(assignment => assignment.IamId == iamId);

        if (!isAdmin)
        {
            return [];
        }

        return [AdminRole];
    }

    public async Task<ClaimsPrincipal?> UpdateUserPrincipalIfNeeded(ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return null;
        }

        var currentRoles = await GetRolesForUser(userId);

        var cookieRoles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var changed = currentRoles.Count != cookieRoles.Count ||
                      currentRoles.Except(cookieRoles).Any();

        if (!changed)
        {
            return null;
        }

        var newId = new ClaimsIdentity(principal.Claims, authenticationType: principal.Identity?.AuthenticationType);

        foreach (var roleClaim in newId.FindAll(ClaimTypes.Role).ToList())
        {
            newId.RemoveClaim(roleClaim);
        }

        foreach (var role in currentRoles)
        {
            newId.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(newId);
    }

    private async Task<string?> ResolveIamIdAsync(string userId)
    {
        if (Guid.TryParse(userId, out var entraObjectId))
        {
            var appUserIamId = await _dbContext.AppUsers
                .AsNoTracking()
                .Where(appUser => appUser.EntraObjectId == entraObjectId)
                .Select(appUser => appUser.IamId)
                .SingleOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(appUserIamId))
            {
                return NormalizeIamId(appUserIamId);
            }
        }

        return NormalizeIamId(userId);
    }

    private static string? NormalizeIamId(string? iamId)
    {
        var normalized = iamId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool ApplyExistingUserUpdates(
        AppUser user,
        string? displayName,
        string? email,
        string? iamId,
        string? resolvedEmployeeId,
        bool recordSignIn,
        DateTime now)
    {
        var shouldSaveUser = false;

        if (!string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
        {
            user.DisplayName = displayName;
            shouldSaveUser = true;
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = email;
            shouldSaveUser = true;
        }

        if (!string.IsNullOrWhiteSpace(iamId) &&
            !string.Equals(user.IamId, iamId, StringComparison.OrdinalIgnoreCase))
        {
            user.IamId = iamId;
            shouldSaveUser = true;
        }

        if (!string.IsNullOrWhiteSpace(resolvedEmployeeId) &&
            !string.Equals(user.EmployeeId, resolvedEmployeeId, StringComparison.OrdinalIgnoreCase))
        {
            user.EmployeeId = resolvedEmployeeId;
            shouldSaveUser = true;
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            shouldSaveUser = true;
        }

        if (recordSignIn)
        {
            user.LastLoginUtc = now;
            user.UpdatedUtc = now;
            shouldSaveUser = true;
        }

        return shouldSaveUser;
    }

    private static bool IsConcurrentInsert(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private static string? ResolveIamIdFromClaims(ClaimsPrincipal principal)
    {
        return NormalizeIamIdToken(principal.FindFirst("ucdPersonIAMID")?.Value);
    }

    private static string? NormalizeIamIdToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(compact))
        {
            return null;
        }

        var normalized = compact.Trim().ToLowerInvariant();
        return normalized.Length <= 10 ? normalized : null;
    }

    private async Task<Person?> FindPersonByEmailAsync(string? email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null)
        {
            return null;
        }

        var matches = await _dbContext.People
            .Where(person => person.Email != null && person.Email.ToLower() == normalizedEmail)
            .OrderByDescending(person => person.PromotedAt)
            .ThenByDescending(person => person.ModifyDate)
            .ToListAsync(cancellationToken);

        return matches.FirstOrDefault();
    }

    // if the user does not hace an entry in the people table employeeID is unknown
    private async Task<Person?> FindPersonByIamIdAsync(string? iamId, CancellationToken cancellationToken)
    {
        var normalizedIamId = NormalizeIamIdToken(iamId);
        if (normalizedIamId == null)
        {
            return null;
        }

        var matches = await _dbContext.People
            .Where(person => person.IamId.ToLower() == normalizedIamId)
            .OrderByDescending(person => person.PromotedAt)
            .ThenByDescending(person => person.ModifyDate)
            .ToListAsync(cancellationToken);

        return matches.FirstOrDefault();
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeEmployeeId(string? employeeId)
    {
        var normalized = employeeId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
