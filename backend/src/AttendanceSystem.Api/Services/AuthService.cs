using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceSystem.Api.Services;

public class AuthService(AppDbContext db, IOptions<JwtOptions> jwtOptions, TimeProvider time)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var login = req.Login.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == login || u.UniversityId.ToLower() == login, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Result.Fail<AuthResponse>(401, "Invalid credentials.");
        return Result.Success(await IssueAsync(user, ct));
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == req.RefreshToken, ct);
        if (stored is null || stored.Revoked || stored.ExpiresAt <= now)
            return Result.Fail<AuthResponse>(401, "Refresh token is invalid or expired.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null) return Result.Fail<AuthResponse>(401, "User no longer exists.");
        stored.Revoked = true;                       // rotate: one-time use
        return Result.Success(await IssueAsync(user, ct));
    }

    public async Task<ServiceResult<bool>> RegisterDeviceAsync(Guid userId, string token, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail<bool>(404, "User not found.");
        user.FcmToken = token;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwt.AccessMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("name", user.FullName),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var jwt = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, now, expires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var access = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = refresh, ExpiresAt = now.AddDays(_jwt.RefreshDays) });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(access, refresh, expires,
            new UserDto(user.Id, user.UniversityId, user.FullName, user.Email, user.Role.ToString()));
    }
}
