using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using ETCS.Shared.Auth;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Auth.Models;
using ETCS.Shared.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ETCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly JwtOptions _jwtOptions;
    private readonly IParentLoginRepository _parentLoginRepository;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public AuthController(
        IOptions<JwtOptions> jwtOptions,
        IParentLoginRepository parentLoginRepository,
        IRefreshTokenStore refreshTokenStore)
    {
        _jwtOptions = jwtOptions.Value;
        _parentLoginRepository = parentLoginRepository;
        _refreshTokenStore = refreshTokenStore;
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { message = "First name and last name are required." });
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { message = "Username is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
        {
            return BadRequest(new { message = "Mobile number is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Password is required." });
        }

        var result = await _parentLoginRepository.RegisterAsync(request, cancellationToken);
        if (!result.IsSuccess || result.User is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new RegisterResponse(result.GuardianId, result.User));
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("token")]
    public async Task<IActionResult> CreateToken([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest(new { message = "Username is required." });
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Password is required." });
        }

        var loginName = request.UserName.Trim();
        var loginRow = await _parentLoginRepository.GetLoginAsync(loginName, cancellationToken);
        if (!loginRow.SpIndicatesSuccess)
        {
            return Unauthorized(new { message = "Invalid login." });
        }

        if (string.IsNullOrEmpty(loginRow.StoredPasswordOrHash))
        {
            return Unauthorized(new { message = "Invalid login." });
        }

        var hashedInput = SecurityHelper.GetMd5Hash(request.Password);
        if (!string.Equals(hashedInput, loginRow.StoredPasswordOrHash, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { message = "Invalid login." });
        }

        var (accessToken, expiresAtUtc) = CreateAccessToken(loginRow.id, loginName);
        var refreshToken = CreateRefreshToken();
        var refreshExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        await _refreshTokenStore.SaveAsync(refreshToken, new RefreshTokenRecord
        {
            Id = loginRow.id,
            Username = loginName,
            ExpiresAtUtc = refreshExpiresAtUtc
        }, cancellationToken);

        return Ok(new AuthTokenResponse(accessToken, "Bearer", expiresAtUtc, refreshToken, refreshExpiresAtUtc, loginRow.User));
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var record = await _refreshTokenStore.GetAsync(request.RefreshToken, cancellationToken);
        if (record is null || !record.IsActive)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        await _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);

        var (accessToken, expiresAtUtc) = CreateAccessToken(record.Id, record.Username);
        var newRefreshToken = CreateRefreshToken();
        var refreshExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        await _refreshTokenStore.SaveAsync(newRefreshToken, new RefreshTokenRecord
        {
            Id = record.Id,
            Username = record.Username,
            ExpiresAtUtc = refreshExpiresAtUtc
        }, cancellationToken);

        return Ok(new AuthTokenResponse(accessToken, "Bearer", expiresAtUtc, newRefreshToken, refreshExpiresAtUtc, null));
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
        return Ok(new { message = "Refresh token revoked." });
    }

    private (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(int id, string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(JwtRegisteredClaimNames.Sub, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "ApiUser")
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
