using hhSalonAPI.Domain.Concrete;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace hhSalon.Tests.Integration
{
	[NonParallelizable]
	public class ApiAuthenticationIntegrationTests
	{
		private SalonApiFactory _factory = null!;
		private HttpClient _client = null!;

		[SetUp]
		public async Task SetUp()
		{
			_factory = new SalonApiFactory();
			_client = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false,
				BaseAddress = new Uri("https://localhost"),
				HandleCookies = true
			});
			await _factory.EnsureDatabaseCreated();
		}

		[TearDown]
		public void TearDown()
		{
			_client.Dispose();
			_factory.Dispose();
		}

		[Test]
		public async Task ProtectedApiAndSignalRNegotiateRejectAnonymousRequests()
		{
			var apiResponse = await _client.GetAsync("/api/Attendances/all-attendances");
			var hubResponse = await _client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);

			Assert.That(apiResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
			Assert.That(hubResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
		}

		[Test]
		public async Task IssuedSecureCookieAuthenticatesApiAndSignalRButQueryTokenIsHubOnly()
		{
			var registration = await _client.PostAsJsonAsync("/api/Auth/register", new
			{
				firstName = "Integration",
				lastName = "User",
				email = "integration@example.com",
				userName = "integration-user",
				password = "Strong1!"
			});
			Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.OK));

			var login = await _client.PostAsJsonAsync("/api/Auth/authenticate", new
			{
				userName = "integration-user",
				password = "Strong1!"
			});
			Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

			var cookies = login.Headers.GetValues("Set-Cookie").ToArray();
			var accessCookie = cookies.Single(value => value.StartsWith("hhSalon.access_token=", StringComparison.Ordinal));
			var refreshCookie = cookies.Single(value => value.StartsWith("hhSalon.refresh_token=", StringComparison.Ordinal));
			Assert.That(accessCookie, Does.Contain("httponly").IgnoreCase);
			Assert.That(accessCookie, Does.Contain("secure").IgnoreCase);
			Assert.That(accessCookie, Does.Contain("samesite=strict").IgnoreCase);
			Assert.That(refreshCookie, Does.Contain("httponly").IgnoreCase);
			Assert.That(refreshCookie, Does.Contain("secure").IgnoreCase);
			Assert.That(refreshCookie, Does.Contain("samesite=strict").IgnoreCase);

			var authenticated = await _client.GetAsync("/api/Auth/me");
			Assert.That(authenticated.StatusCode, Is.EqualTo(HttpStatusCode.OK));

			var accessToken = Uri.UnescapeDataString(accessCookie["hhSalon.access_token=".Length..].Split(';')[0]);
			using var noCookieClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
			{
				AllowAutoRedirect = false,
				BaseAddress = new Uri("https://localhost"),
				HandleCookies = false
			});
			var tokenOutsideHub = await noCookieClient.GetAsync($"/api/Auth/me?access_token={Uri.EscapeDataString(accessToken)}");
			var tokenAtHub = await noCookieClient.PostAsync(
				$"/hubs/chat/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(accessToken)}", null);

			Assert.That(tokenOutsideHub.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
			Assert.That(tokenAtHub.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		}

		private sealed class SalonApiFactory : WebApplicationFactory<Program>
		{
			private readonly SqliteConnection _connection = new("DataSource=:memory:");

			protected override void ConfigureWebHost(IWebHostBuilder builder)
			{
				builder.UseEnvironment("Testing");
				builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
					new Dictionary<string, string?>
					{
						["ConnectionStrings:DefaultConnectionString"] = "server=localhost;user=test;password=test;database=test",
						["Jwt:Key"] = "integration-test-signing-key-longer-than-32-bytes",
						["Jwt:Issuer"] = "hhSalonAPI",
						["Jwt:Audience"] = "hhSalonClient",
						["Jwt:AccessTokenMinutes"] = "30",
						["Jwt:RefreshTokenDays"] = "5",
						["Database:SeedOnStartup"] = "false",
						["BootstrapAdmin:Enabled"] = "false",
						["Cors:AllowedOrigins:0"] = "https://localhost:4200"
					}));

				builder.ConfigureServices(services =>
				{
					var databaseServices = services.Where(descriptor =>
						descriptor.ServiceType == typeof(AppDbContext)
						|| descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>)
						|| (descriptor.ServiceType.IsGenericType
							&& descriptor.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))
							&& descriptor.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)))
						.ToList();
					foreach (var descriptor in databaseServices)
						services.Remove(descriptor);

					_connection.Open();
					services.AddSingleton(_connection);
					services.AddDbContext<AppDbContext>((provider, options) =>
						options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
				});
			}

			public async Task EnsureDatabaseCreated()
			{
				using var scope = Services.CreateScope();
				var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
				await context.Database.EnsureCreatedAsync();
			}

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				if (disposing)
					_connection.Dispose();
			}
		}
	}
}
