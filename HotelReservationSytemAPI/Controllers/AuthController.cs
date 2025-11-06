using HotelReservationSytemAPI.DTOs;
using HotelReservationSytemAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext context,
        IPasswordService passwordService,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(registerDto.Username) ||
                string.IsNullOrWhiteSpace(registerDto.Email) ||
                string.IsNullOrWhiteSpace(registerDto.Password))
            {
                return BadRequest("All fields are required");
            }

            // Check if username already exists
            if (await _context.Users.AnyAsync(u => u.Username == registerDto.Username))
            {
                return BadRequest("Username already exists");
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
            {
                return BadRequest("Email already exists");
            }

            // Create new user
            var user = new User
            {
                Username = registerDto.Username.Trim(),
                Email = registerDto.Email.Trim().ToLower(),
                PasswordHash = _passwordService.HashPassword(registerDto.Password),
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Generate token
            var token = _tokenService.GenerateToken(user);
            //Generate refresh token
            var RefreshRokenEntity = _tokenService.GenerateRefreshToken(user);
            var refreshToken = RefreshRokenEntity.Token;
            _context.RefreshTokens.Add(RefreshRokenEntity);
            await _context.SaveChangesAsync();

            var userResponse = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            var response = new AuthResponseDto
            {
                Token = token,
                ResfreshToken = refreshToken,
                User = userResponse,
                ExpiresAt = DateTime.UtcNow.AddMinutes(180) // 3 hours
            };

            _logger.LogInformation("User registered successfully: {Email}", user.Email);

            return Ok(new { message = "User registered successfully", data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, "An error occurred during registration");
        }
    }

    /// <summary>
    /// Login user with email and password
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(loginDto.Email) ||
                string.IsNullOrWhiteSpace(loginDto.Password))
            {
                return BadRequest("Email and password are required");
            }

            // Find user by email (case-insensitive)
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == loginDto.Email.Trim().ToLower());

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found - {Email}", loginDto.Email);
                return Unauthorized("Invalid email or password");
            }

            // Verify password
            if (!_passwordService.VerifyPassword(loginDto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password for user - {Email}", loginDto.Email);
                return Unauthorized("Invalid email or password");
            }

            // Check if password needs rehashing (if work factor changed)
            if (_passwordService.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = _passwordService.HashPassword(loginDto.Password);
                await _context.SaveChangesAsync();
            }

            // Generate token
            var token = _tokenService.GenerateToken(user);
            //Generate refresh token
            var RefreshRokenEntity = _tokenService.GenerateRefreshToken(user);
            var refreshToken = RefreshRokenEntity.Token;
            _context.RefreshTokens.Add(RefreshRokenEntity);
            await _context.SaveChangesAsync();

            var userResponse = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            var response = new AuthResponseDto
            {
                Token = token,
                ResfreshToken = refreshToken,
                User = userResponse,
                ExpiresAt = DateTime.UtcNow.AddMinutes(180) // 3 hours
            };

            _logger.LogInformation("User logged in successfully: {Email}", user.Email);

            return Ok(new { message = "Login successful", data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return StatusCode(500, "An error occurred during login");
        }
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User not found");
            }

            var userResponse = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return Ok(new { data = userResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return StatusCode(500, "An error occurred while getting profile");
        }
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto changePasswordDto)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User not found");
            }

            // Verify current password
            if (!_passwordService.VerifyPassword(changePasswordDto.CurrentPassword, user.PasswordHash))
            {
                return BadRequest("Current password is incorrect");
            }

            // Hash new password
            user.PasswordHash = _passwordService.HashPassword(changePasswordDto.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password changed successfully for user: {Email}", user.Email);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, "An error occurred while changing password");
        }
    }
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest("Refresh token is required");

            // Find refresh token and ensure it matches the user
            var existingToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == request.userId);

            if (existingToken == null)
                return Unauthorized("Invalid refresh token or user mismatch");

            if (existingToken.ExpiresAt < DateTime.UtcNow)
                return Unauthorized("Refresh token has expired");

            // Get the associated user
            var user = await _context.Users.FindAsync(existingToken.UserId);
            if (user == null)
                return Unauthorized("User not found");

            // OPTIONAL: Token rotation — remove old token, generate a new one
            _context.RefreshTokens.Remove(existingToken);

            var newRefreshTokenEntity = _tokenService.GenerateRefreshToken(user);
            _context.RefreshTokens.Add(newRefreshTokenEntity);

            var newAccessToken = _tokenService.GenerateToken(user);

            await _context.SaveChangesAsync();

            var response = new
            {
                Token = newAccessToken,
                RefreshToken = newRefreshTokenEntity.Token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(180)
            };

            return Ok(new { message = "Token refreshed successfully", data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, "An error occurred while refreshing the token");
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            // Extract the JWT from the Authorization header
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "").Trim();

            if (string.IsNullOrEmpty(token))
                return BadRequest("No token provided");

            // Check if already blacklisted
            if (await _context.BlackListTokens.AnyAsync(t => t.Token == token))
                return Ok(new { message = "Token already invalidated" });

            // Add to blacklist
            var blacklisted = new BlackListToken
            {
                Token = token,
                UserId = userId,
                BlacklistedAt = DateTime.UtcNow
            };

            _context.BlackListTokens.Add(blacklisted);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} logged out successfully", userId);

            return Ok(new { message = "Logout successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "An error occurred while logging out");
        }
    }


}