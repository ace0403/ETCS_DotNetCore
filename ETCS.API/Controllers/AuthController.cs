using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using ETCS.API.Infrastructure.Auth;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Auth;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Auth.Models;
using ETCS.Shared.Options;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly JwtOptions _jwtOptions;
    private readonly ParentPortalOptions _parentPortalOptions;
    private readonly IParentLoginRepository _parentLoginRepository;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IRegistrationOtpService _registrationOtpService;
    private readonly IDeleteAccountOtpService _deleteAccountOtpService;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptions<JwtOptions> jwtOptions,
        IOptions<ParentPortalOptions> parentPortalOptions,
        IParentLoginRepository parentLoginRepository,
        IRefreshTokenStore refreshTokenStore,
        IRegistrationOtpService registrationOtpService,
        IDeleteAccountOtpService deleteAccountOtpService,
        IGuardianEmailNotificationService emailNotificationService,
        ILogger<AuthController> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _parentPortalOptions = parentPortalOptions.Value;
        _parentLoginRepository = parentLoginRepository;
        _refreshTokenStore = refreshTokenStore;
        _registrationOtpService = registrationOtpService;
        _deleteAccountOtpService = deleteAccountOtpService;
        _emailNotificationService = emailNotificationService;
        _logger = logger;
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var result = await _registrationOtpService.SendOtpAsync(request.Email, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new SendOtpResponse(result.Message, result.ExpiresInSeconds));
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { message = "Verification code is required." });
        }

        var result = await _registrationOtpService.VerifyOtpAsync(request.Email, request.Otp, cancellationToken);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.VerificationToken))
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new VerifyOtpResponse(result.VerificationToken, result.ExpiresInSeconds));
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

        if (string.IsNullOrWhiteSpace(request.VerificationToken))
        {
            return BadRequest(new { message = "Email verification is required to complete registration." });
        }

        var verification = await _registrationOtpService.ValidateVerificationTokenAsync(
            request.Email,
            request.VerificationToken,
            cancellationToken);
        if (!verification.IsSuccess)
        {
            return BadRequest(new { message = verification.Message });
        }

        var result = await _parentLoginRepository.RegisterAsync(request, cancellationToken);
        if (!result.IsSuccess || result.User is null)
        {
            return BadRequest(new { message = result.Message });
        }

        await _registrationOtpService.MarkVerificationTokenUsedAsync(request.VerificationToken, cancellationToken);

        var guardianName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim();
        var addChildLink = BuildAddChildLink();
        if (string.IsNullOrWhiteSpace(addChildLink))
        {
            _logger.LogWarning(
                "Registration succeeded for {Email} but ParentPortal:PublicBaseUrl is not configured; skipping registration success email.",
                request.Email);
        }
        else
        {
            await _emailNotificationService.QueueRegistrationSuccessAsync(
                request.Email,
                guardianName,
                addChildLink,
                cancellationToken);
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

        if (await _parentLoginRepository.IsAccountDeletedAsync(loginRow.id, cancellationToken))
        {
            return Unauthorized(new { message = "This account has been deleted." });
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

        if (await _parentLoginRepository.IsAccountDeletedAsync(record.Id, cancellationToken))
        {
            await _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
            return Unauthorized(new { message = "This account has been deleted." });
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

    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("delete-account/send-otp")]
    public async Task<IActionResult> SendDeleteAccountOtp(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var result = await _deleteAccountOtpService.SendOtpAsync(guardianId, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            expiresInSeconds = result.ExpiresInSeconds,
            maskedEmail = result.MaskedEmail
        });
    }

    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { message = "Verification code is required." });
        }

        var otpResult = await _deleteAccountOtpService.VerifyOtpAsync(guardianId, request.Otp, cancellationToken);
        if (!otpResult.IsSuccess)
        {
            return BadRequest(new { message = otpResult.Message });
        }

        var result = await _parentLoginRepository.SoftDeleteAccountAsync(guardianId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        await _refreshTokenStore.RevokeAllByUserIdAsync(guardianId, cancellationToken);
        return Ok(new { message = result.Message });
    }

    private string? BuildAddChildLink()
    {
        var baseUrl = (_parentPortalOptions.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/MyKids";
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
