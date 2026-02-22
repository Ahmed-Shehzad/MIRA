using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Auth;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(ApplicationDbContext db, IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponse?> GetTestTokenAsync(TestTokenRequest request, CancellationToken cancellationToken = default)
    {
        var defaultSlug = (TenantSlug)DbInitializer.DefaultTenantSlug;
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == defaultSlug, cancellationToken);
        if (tenant == null)
            return null;

        var userId = (UserId)$"test-{Guid.NewGuid():N}";
        var user = new AppUser
        {
            Id = userId,
            CognitoUsername = request.Email,
            Email = (Email)request.Email,
            Company = request.Company,
            TenantId = tenant.Id,
            Groups = [(UserGroup)DbInitializer.GroupUsers],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.Generate(user);
        return new AuthResponse(token, user.Email.Value, user.Company, user.Groups.Select(g => g.Value).ToList());
    }

    public async Task<AuthResponse?> GetCurrentUserAsync(string userId, string? bearerToken, CancellationToken cancellationToken = default)
    {
        var uid = (UserId)userId;
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == uid, cancellationToken);
        if (user == null || string.IsNullOrEmpty(bearerToken))
            return null;

        return new AuthResponse(bearerToken, user.Email.Value, user.Company, user.Groups.Select(g => g.Value).ToList());
    }
}
