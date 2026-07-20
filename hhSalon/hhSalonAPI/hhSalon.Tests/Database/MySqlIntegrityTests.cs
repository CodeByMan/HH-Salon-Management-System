using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Enums;
using hhSalon.Domain.Entities.Static;
using hhSalonAPI.Domain.Concrete;
using Microsoft.EntityFrameworkCore;

namespace hhSalon.Tests.Database
{
	[NonParallelizable]
	[Category("MySqlIntegration")]
	public class MySqlIntegrityTests
	{
		[Test]
		public async Task MySqlMigrationEnforcesConcurrentWorkerSlotAndAccountUniqueness()
		{
			var connectionString = Environment.GetEnvironmentVariable("HH_TEST_MYSQL_CONNECTION");
			if (string.IsNullOrWhiteSpace(connectionString))
				Assert.Ignore("HH_TEST_MYSQL_CONNECTION is not configured.");

			var options = new DbContextOptionsBuilder<AppDbContext>().UseMySQL(connectionString!).Options;
			await using (var setup = new AppDbContext(options))
			{
				await setup.Database.EnsureDeletedAsync();
				await setup.Database.MigrateAsync();
				setup.Users.AddRange(
					User("client-1", "client1@example.com", UserRoles.Client),
					User("client-2", "client2@example.com", UserRoles.Client),
					User("worker", "worker@example.com", UserRoles.Worker));
				setup.Workers.Add(new Worker { Id = "worker", Address = "Test", Gender = "Other" });
				await setup.SaveChangesAsync();
			}

			var insertResults = await Task.WhenAll(
				TryInsertAppointment(options, "client-1"),
				TryInsertAppointment(options, "client-2"));
			Assert.That(insertResults.Count(result => result), Is.EqualTo(1),
				"The worker/date/time unique index must allow exactly one concurrent insert.");

			await using (var duplicateEmail = new AppDbContext(options))
			{
				duplicateEmail.Users.Add(User("different-username", "client1@example.com", UserRoles.Client));
				Assert.ThrowsAsync<DbUpdateException>(() => duplicateEmail.SaveChangesAsync());
			}

			await using (var duplicateUserName = new AppDbContext(options))
			{
				var duplicate = User("client-1", "different@example.com", UserRoles.Client);
				duplicate.Id = "different-id";
				duplicateUserName.Users.Add(duplicate);
				Assert.ThrowsAsync<DbUpdateException>(() => duplicateUserName.SaveChangesAsync());
			}
		}

		private static async Task<bool> TryInsertAppointment(DbContextOptions<AppDbContext> options, string clientId)
		{
			await using var context = new AppDbContext(options);
			context.Attendances.Add(Appointment(clientId));
			try
			{
				await context.SaveChangesAsync();
				return true;
			}
			catch (DbUpdateException)
			{
				return false;
			}
		}

		private static Attendance Appointment(string clientId) => new()
		{
			ClientId = clientId,
			WorkerId = "worker",
			Date = new DateTime(2030, 1, 2),
			Time = TimeSpan.FromHours(10),
			Price = 20m,
			IsPaid = YesNo.No,
			IsRendered = YesNo.No
		};

		private static User User(string userName, string email, string role) => new()
		{
			Id = userName,
			FirstName = "Test",
			LastName = "User",
			UserName = userName,
			Email = email,
			Password = "test-hash",
			Role = role,
			Token = string.Empty
		};
	}
}
