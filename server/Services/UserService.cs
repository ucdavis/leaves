using System.Security.Claims;
using System.Security.Cryptography;
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

    Task<string?> GetDisplayNameForUser(
        string userId,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetRolesForUser(
        string userId,
        CancellationToken cancellationToken = default);
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

        var resolvedAuthorizationKey = NormalizeAuthorizationKey(matchedPerson?.IamId) ?? existingUser?.IamId;
        if (string.IsNullOrWhiteSpace(resolvedAuthorizationKey))
        {
            resolvedAuthorizationKey = ResolveAuthorizationKey(principal, entraObjectId);
        }
        var resolvedEmployeeId = NormalizeEmployeeId(matchedPerson?.EmployeeId) ?? existingUser?.EmployeeId;
        var resolvedDisplayName = !string.IsNullOrWhiteSpace(matchedPerson?.FullName)
            ? matchedPerson.FullName
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
                IamId = resolvedAuthorizationKey,
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
                resolvedAuthorizationKey,
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
                    resolvedAuthorizationKey,
                    resolvedEmployeeId,
                    recordSignIn,
                    now))
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    public async Task<string?> GetDisplayNameForUser(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var entraObjectId))
        {
            return null;
        }

        return await _dbContext.AppUsers
            .AsNoTracking()
            .Where(appUser => appUser.EntraObjectId == entraObjectId)
            .Select(appUser => appUser.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<List<string>> GetRolesForUser(
        string userId,
        CancellationToken cancellationToken = default)
    {
        bool isAdmin;
        if (Guid.TryParse(userId, out var entraObjectId))
        {
            isAdmin = await (
                    from appUser in _dbContext.AppUsers.AsNoTracking()
                    join assignment in _dbContext.AppAdminAssignments.AsNoTracking()
                        on appUser.IamId equals assignment.IamId
                    where appUser.EntraObjectId == entraObjectId
                    select assignment.Id)
                .AnyAsync(cancellationToken);
        }
        else
        {
            var iamId = NormalizeIamId(userId);
            if (iamId == null)
            {
                _logger.LogDebug(
                    "Could not resolve an IAM ID for user {UserId}; no app roles will be added.",
                    userId);
                return [];
            }

            isAdmin = await _dbContext.AppAdminAssignments
                .AsNoTracking()
                .AnyAsync(assignment => assignment.IamId == iamId, cancellationToken);
        }

        if (!isAdmin)
        {
            return [];
        }

        return [AdminRole];
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
        string? resolvedAuthorizationKey,
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

        if (!string.IsNullOrWhiteSpace(resolvedAuthorizationKey) &&
            !string.Equals(user.IamId, resolvedAuthorizationKey, StringComparison.OrdinalIgnoreCase))
        {
            user.IamId = resolvedAuthorizationKey;
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

    private string ResolveAuthorizationKey(ClaimsPrincipal principal, Guid entraObjectId)
    {
        var directClaim =
            NormalizeAuthorizationKey(principal.FindFirst("iam_id")?.Value)
            ?? NormalizeAuthorizationKey(principal.FindFirst("iamid")?.Value);
        if (!string.IsNullOrWhiteSpace(directClaim))
        {
            return directClaim;
        }

        return BuildSyntheticAuthorizationKey(entraObjectId);
    }

    private static string? NormalizeAuthorizationKey(string? value)
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

        return await _dbContext.People
            .AsNoTracking()
            .Where(person => person.Email == normalizedEmail)
            .OrderByDescending(person => person.PromotedAt)
            .ThenByDescending(person => person.ModifyDate)
            .FirstOrDefaultAsync(cancellationToken);
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

    public static string BuildSyntheticAuthorizationKey(Guid entraObjectId)
    {
        var hash = SHA256.HashData(entraObjectId.ToByteArray());
        return Convert.ToHexString(hash.AsSpan(0, 5)).ToLowerInvariant();
    }
}
