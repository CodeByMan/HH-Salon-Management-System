using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Domain.Concrete;
using hhSalonAPI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace hhSalonAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class AuthController : ControllerBase
	{
		private const string AccessCookieName = "hhSalon.access_token";
		private const string RefreshCookieName = "hhSalon.refresh_token";
		private const string GenericRegistrationMessage = "Registration could not be completed.";
		private readonly AppDbContext _context;
		private readonly IConfiguration _configuration;
		private readonly IEmailService _emailService;

		public AuthController(AppDbContext context, IConfiguration configuration, IEmailService emailService)
		{
			_context = context;
			_configuration = configuration;
			_emailService = emailService;
		}

		[AllowAnonymous]
		[EnableRateLimiting("authentication")]
		[HttpPost("authenticate")]
		public async Task<IActionResult> Authenticate([FromBody] LoginDto login)
		{
			var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == login.UserName);
			if (user == null || !PasswordHasher.VerifyPassword(login.Password, user.Password))
				return Unauthorized(new { Message = "Invalid username or password." });

			await StartSession(user);
			return Ok(ToAuthenticatedUser(user));
		}

		[Authorize]
		[HttpGet("me")]
		public async Task<IActionResult> Me()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
			return user == null ? Unauthorized() : Ok(ToAuthenticatedUser(user));
		}

		[AllowAnonymous]
		[EnableRateLimiting("authentication")]
		[HttpPost("register")]
		public async Task<IActionResult> RegisterUser([FromBody] RegisterUserDto userDto)
		{
			var validationResult = await ValidateRegistration(userDto);
			if (validationResult != null)
				return validationResult;

			return await SaveRegisteredUser(CreateUser(userDto, UserRoles.Client), "User registered!");
		}

		[Authorize(Roles = UserRoles.Admin)]
		[EnableRateLimiting("authentication")]
		[HttpPost("admin-register")]
		public async Task<IActionResult> RegisterAdmin([FromBody] RegisterUserDto userDto)
		{
			var validationResult = await ValidateRegistration(userDto);
			if (validationResult != null)
				return validationResult;

			return await SaveRegisteredUser(CreateUser(userDto, UserRoles.Admin), "Administrator registered!");
		}

		[Authorize(Roles = UserRoles.Admin)]
		[EnableRateLimiting("authentication")]
		[HttpPost("worker-register")]
		public async Task<IActionResult> RegisterWorker([FromBody] RegisterWorkerDto workerDto)
		{
			var validationResult = await ValidateRegistration(workerDto);
			if (validationResult != null)
				return validationResult;

			var groupIds = workerDto.GroupsIds.Distinct().ToList();
			if (groupIds.Count == 0 || await _context.Groups.CountAsync(g => groupIds.Contains(g.Id)) != groupIds.Count)
				return BadRequest(new { Message = "Select one or more valid service groups." });

			var user = CreateUser(workerDto, UserRoles.Worker);
			var worker = new Worker { Id = user.Id, Address = workerDto.Address, Gender = workerDto.Gender };

			await using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				await _context.Users.AddAsync(user);
				await _context.Workers.AddAsync(worker);
				await _context.Workers_Groups.AddRangeAsync(groupIds.Select(groupId => new WorkerGroup
				{
					WorkerId = user.Id,
					GroupId = groupId
				}));
				await _context.SaveChangesAsync();
				await transaction.CommitAsync();
				return Ok(new { Message = "Worker registered!", WorkerId = user.Id });
			}
			catch (DbUpdateException)
			{
				await transaction.RollbackAsync();
				return Conflict(new { Message = GenericRegistrationMessage });
			}
		}

		[AllowAnonymous]
		[EnableRateLimiting("refresh")]
		[HttpPost("refresh")]
		public async Task<IActionResult> Refresh()
		{
			if (!Request.Cookies.TryGetValue(RefreshCookieName, out var suppliedRefreshToken) || string.IsNullOrWhiteSpace(suppliedRefreshToken))
				return Unauthorized(new { Message = "Refresh token is expired or revoked." });

			var hash = HashToken(suppliedRefreshToken);
			var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash);
			if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
			{
				ClearSessionCookies();
				return Unauthorized(new { Message = "Refresh token is expired or revoked." });
			}

			await StartSession(user);
			return Ok(ToAuthenticatedUser(user));
		}

		[Authorize]
		[HttpPost("logout")]
		public async Task<IActionResult> Logout()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
			if (user != null)
			{
				user.Token = string.Empty;
				user.RefreshTokenHash = null;
				user.RefreshTokenExpiryTime = DateTime.UnixEpoch;
				await _context.SaveChangesAsync();
			}

			ClearSessionCookies();
			return Ok(new { Message = "Logged out." });
		}

		[AllowAnonymous]
		[EnableRateLimiting("authentication")]
		[HttpPost("send-reset-email/{email}")]
		public async Task<IActionResult> SendEmail(string email)
		{
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
			if (user != null)
			{
				var emailToken = CreateRandomToken();
				user.ResetPasswordTokenHash = HashToken(emailToken);
				user.ResetPasswordExpiry = DateTime.UtcNow.AddMinutes(15);
				await _context.SaveChangesAsync();

				var emailModel = new EmailModel(email, "Reset Password", EmailBody.EmailStringBody(email, emailToken));
				_emailService.SendEmail(emailModel);
			}

			return Ok(new { StatusCode = 200, Message = "If the account exists, a reset email has been sent." });
		}

		[AllowAnonymous]
		[EnableRateLimiting("authentication")]
		[HttpPost("reset-password")]
		public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
		{
			if (resetPasswordDto.NewPassword != resetPasswordDto.ConfirmPassword)
				return BadRequest(new { Message = "Passwords do not match." });

			var suppliedToken = resetPasswordDto.EmailToken?.Replace(" ", "+");
			var suppliedHash = string.IsNullOrWhiteSpace(suppliedToken) ? null : HashToken(suppliedToken);
			var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == resetPasswordDto.Email);
			if (user == null || user.ResetPasswordExpiry < DateTime.UtcNow || !HashesMatch(user.ResetPasswordTokenHash, suppliedHash))
				return BadRequest(new { StatusCode = 400, Message = "Invalid reset link." });

			var passwordError = CheckPasswordStrength(resetPasswordDto.NewPassword);
			if (!string.IsNullOrEmpty(passwordError))
				return BadRequest(new { Message = passwordError });

			user.Password = PasswordHasher.HashPassword(resetPasswordDto.NewPassword);
			user.ResetPasswordTokenHash = null;
			user.ResetPasswordExpiry = DateTime.UnixEpoch;
			user.RefreshTokenHash = null;
			user.RefreshTokenExpiryTime = DateTime.UnixEpoch;
			await _context.SaveChangesAsync();
			ClearSessionCookies();

			return Ok(new { StatusCode = 200, Message = "Password reset successfully." });
		}

		private async Task<IActionResult> SaveRegisteredUser(User user, string successMessage)
		{
			try
			{
				await _context.Users.AddAsync(user);
				await _context.SaveChangesAsync();
				return Ok(new { Message = successMessage });
			}
			catch (DbUpdateException)
			{
				return Conflict(new { Message = GenericRegistrationMessage });
			}
		}

		private async Task<IActionResult> ValidateRegistration(RegisterUserDto userDto)
		{
			if (await _context.Users.AnyAsync(u => u.UserName == userDto.UserName || u.Email == userDto.Email))
				return Conflict(new { Message = GenericRegistrationMessage });

			var passwordError = CheckPasswordStrength(userDto.Password);
			return string.IsNullOrEmpty(passwordError) ? null : BadRequest(new { Message = passwordError });
		}

		private static User CreateUser(RegisterUserDto userDto, string role)
		{
			if (role != UserRoles.Client && role != UserRoles.Worker && role != UserRoles.Admin)
				throw new InvalidOperationException("Invalid server-side role assignment.");

			return new User
			{
				Id = Guid.NewGuid().ToString(),
				FirstName = userDto.FirstName,
				LastName = userDto.LastName,
				Email = userDto.Email,
				UserName = userDto.UserName,
				Password = PasswordHasher.HashPassword(userDto.Password),
				Role = role,
				Token = string.Empty
			};
		}

		private async Task StartSession(User user)
		{
			var accessToken = CreateJwt(user);
			var refreshToken = CreateRandomToken();
			user.Token = string.Empty;
			user.RefreshTokenHash = HashToken(refreshToken);
			user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(GetRefreshTokenDays());
			await _context.SaveChangesAsync();

			Response.Cookies.Append(AccessCookieName, accessToken, CreateCookieOptions(DateTimeOffset.UtcNow.AddMinutes(GetAccessTokenMinutes())));
			Response.Cookies.Append(RefreshCookieName, refreshToken, CreateCookieOptions(DateTimeOffset.UtcNow.AddDays(GetRefreshTokenDays())));
		}

		private CookieOptions CreateCookieOptions(DateTimeOffset expires)
			=> new()
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Strict,
				Expires = expires,
				Path = "/"
			};

		private void ClearSessionCookies()
		{
			var options = CreateCookieOptions(DateTimeOffset.UnixEpoch);
			Response.Cookies.Delete(AccessCookieName, options);
			Response.Cookies.Delete(RefreshCookieName, options);
		}

		private static AuthenticatedUserDto ToAuthenticatedUser(User user)
			=> new()
			{
				Id = user.Id,
				UserName = user.UserName,
				FullName = $"{user.FirstName} {user.LastName}".Trim(),
				Role = user.Role
			};

		private string CreateJwt(User user)
		{
			var claims = new[]
			{
				new Claim(ClaimTypes.Role, user.Role),
				new Claim(ClaimTypes.Name, user.UserName),
				new Claim(ClaimTypes.NameIdentifier, user.Id)
			};

			var token = new JwtSecurityToken(
				issuer: _configuration["Jwt:Issuer"],
				audience: _configuration["Jwt:Audience"],
				claims: claims,
				notBefore: DateTime.UtcNow,
				expires: DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes()),
				signingCredentials: new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256));

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		private SymmetricSecurityKey GetSigningKey()
		{
			var signingKey = _configuration["Jwt:Key"];
			if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
				throw new InvalidOperationException("Jwt:Key must be configured with at least 32 bytes.");
			return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
		}

		private static string CreateRandomToken()
			=> Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

		private static string HashToken(string token)
			=> Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

		private static bool HashesMatch(string storedHash, string suppliedHash)
		{
			if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(suppliedHash))
				return false;
		return CryptographicOperations.FixedTimeEquals(
				Encoding.ASCII.GetBytes(storedHash),
				Encoding.ASCII.GetBytes(suppliedHash));
		}

		private int GetAccessTokenMinutes()
			=> int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var minutes) && minutes > 0 ? minutes : 30;

		private int GetRefreshTokenDays()
			=> int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var days) && days > 0 ? days : 5;

		private static string CheckPasswordStrength(string password)
		{
			var stringBuilder = new StringBuilder();
			if (string.IsNullOrEmpty(password) || password.Length < 8)
				stringBuilder.Append("Minimum password length should be 8." + Environment.NewLine);
			if (string.IsNullOrEmpty(password) || !(Regex.IsMatch(password, "[a-z]") && Regex.IsMatch(password, "[A-Z]") && Regex.IsMatch(password, "[0-9]")))
				stringBuilder.Append("Password should be alphanumeric." + Environment.NewLine);
			if (string.IsNullOrEmpty(password) || !Regex.IsMatch(password, @"[<,>@!#$%^&*()_+\[\]{}?:;|'./~\-=]"))
				stringBuilder.Append("Password should contain a special character." + Environment.NewLine);
			return stringBuilder.ToString();
		}
	}
}
