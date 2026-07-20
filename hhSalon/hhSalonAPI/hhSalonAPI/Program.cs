using hhSalon.Domain.Concrete;
using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Services.Implementations;
using hhSalon.Services.Services.Interfaces;
using hhSalonAPI.Domain.Concrete;
using hhSalonAPI.Hubs;
using hhSalonAPI.Helpers;
using hhSalonAPI.Payments;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddNewtonsoftJson(options =>
	options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
	throw new InvalidOperationException("ConnectionStrings:DefaultConnectionString must be supplied through environment variables or user secrets.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
	options.UseMySQL(connectionString);
	if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Diagnostics:EnableSensitiveDataLogging"))
		options.EnableSensitiveDataLogging();
});

builder.Services.AddScoped<IGroupsService, GroupsService>();
builder.Services.AddScoped<IServicesService, ServicesService>();
builder.Services.AddScoped<IAttendancesService, AttendancesService>();
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddTransient<IWorkersService, WorkersService>();
builder.Services.AddScoped<IChatDataService, ChatDataService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient<IPayPalService, PayPalService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ChatMessageRateLimiter>();
builder.Services.AddSignalR();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
	?? new[] { "https://localhost:4200" };
builder.Services.AddCors(options => options.AddPolicy("HHOrigins", policy =>
	policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
	throw new InvalidOperationException("Jwt:Key must be supplied securely and contain at least 32 bytes.");
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
	throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

builder.Services.AddAuthentication(options =>
{
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
	options.RequireHttpsMetadata = true;
	options.SaveToken = false;
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
		ValidateAudience = true,
		ValidAudience = jwtAudience,
		ValidateIssuer = true,
		ValidIssuer = jwtIssuer,
		ValidateLifetime = true,
		ClockSkew = TimeSpan.Zero
	};
	options.Events = new JwtBearerEvents
	{
		OnMessageReceived = context =>
		{
			if (context.Request.Cookies.TryGetValue("hhSalon.access_token", out var cookieToken))
				context.Token = cookieToken;

			var queryToken = context.Request.Query["access_token"];
			if (string.IsNullOrWhiteSpace(context.Token)
				&& !string.IsNullOrWhiteSpace(queryToken)
				&& context.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
				context.Token = queryToken;
			return Task.CompletedTask;
		}
	};
});

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
	options.AddPolicy("refresh", context => RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
	options.AddPolicy("payments", context => RateLimitPartition.GetFixedWindowLimiter(
		context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
	options.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
		context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
	options.AddPolicy("chat-connections", context => RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("HHOrigins");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat").RequireRateLimiting("chat-connections");

if (builder.Configuration.GetValue("Database:SeedOnStartup", true))
	AppDbInitializer.Seed(app);

await CreateBootstrapAdministrator(app, builder.Configuration);
app.Run();

static async Task CreateBootstrapAdministrator(WebApplication app, IConfiguration configuration)
{
	if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
		return;

	var userName = configuration["BootstrapAdmin:UserName"];
	var email = configuration["BootstrapAdmin:Email"];
	var password = configuration["BootstrapAdmin:Password"];
	var firstName = configuration["BootstrapAdmin:FirstName"] ?? "System";
	var lastName = configuration["BootstrapAdmin:LastName"] ?? "Administrator";
	if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
		throw new InvalidOperationException("Bootstrap administrator credentials are incomplete.");

	using var scope = app.Services.CreateScope();
	var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	if (await context.Users.AnyAsync(u => u.Role == UserRoles.Admin))
		return;
	if (await context.Users.AnyAsync(u => u.UserName == userName || u.Email == email))
		throw new InvalidOperationException("Bootstrap administrator username or email already belongs to another account.");

	context.Users.Add(new User
	{
		Id = Guid.NewGuid().ToString(),
		FirstName = firstName,
		LastName = lastName,
		Email = email,
		UserName = userName,
		Password = PasswordHasher.HashPassword(password),
		Role = UserRoles.Admin,
		Token = string.Empty
	});
	await context.SaveChangesAsync();
}

public partial class Program { }
