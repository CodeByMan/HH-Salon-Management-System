using hhSalon.Domain.Entities;
using hhSalon.Domain.Entities.Static;
using hhSalon.Services.Services.Implementations;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Controllers;
using hhSalonAPI.Domain.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace hhSalon.Tests.Controllers
{
	public class AttendancesControllerTests
	{
		private AppDbContext _context = null!;
		private AttendancesController _controller = null!;

		[SetUp]
		public void Setup()
		{
			var options = new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase(Guid.NewGuid().ToString())
				.Options;
			_context = new AppDbContext(options);
			SeedDatabase();
			_controller = new AttendancesController(new AttendancesService(_context));
			SetUser("client-1", UserRoles.Client);
		}

		[TearDown]
		public void TearDown()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}

		[Test]
		public async Task NewAttendance_UsesAuthenticatedClientId()
		{
			var appointment = new NewAttendanceVM
			{
				GroupId = 1,
				ServiceId = 1,
				WorkerId = "worker-1",
				Date = Next(DayOfWeek.Monday),
				Time = new TimeSpan(10, 0, 0)
			};

			var result = await _controller.NewAttendance(appointment);

			Assert.That(result, Is.TypeOf<OkResult>());
			Assert.That(_context.Attendances.Single().ClientId, Is.EqualTo("client-1"));
		}

		[Test]
		public async Task ClientCannotReadAnotherClientsAppointments()
		{
			var result = await _controller.MyHistory("client-2");

			Assert.That(result, Is.TypeOf<ForbidResult>());
		}

		[Test]
		public async Task ClientCannotDeleteAnotherClientsAppointment()
		{
			_context.Attendances.Add(new Attendance
			{
				Id = 10,
				ClientId = "client-2",
				WorkerId = "worker-1",
				Date = Next(DayOfWeek.Monday),
				Time = new TimeSpan(11, 0, 0),
				IsPaid = YesNo.No,
				IsRendered = YesNo.No
			});
			_context.SaveChanges();

			var result = await _controller.DeleteAttendance(10);

			Assert.That(result, Is.TypeOf<ForbidResult>());
		}

		private void SetUser(string userId, string role)
		{
			var identity = new ClaimsIdentity(new[]
			{
				new Claim(ClaimTypes.NameIdentifier, userId),
				new Claim(ClaimTypes.Role, role)
			}, "TestAuthentication");
			_controller.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
			};
		}

		private void SeedDatabase()
		{
			_context.Users.AddRange(
				new User { Id = "client-1", UserName = "client1", Role = UserRoles.Client },
				new User { Id = "client-2", UserName = "client2", Role = UserRoles.Client },
				new User { Id = "worker-1", UserName = "worker1", Role = UserRoles.Worker });
			_context.Workers.Add(new Worker { Id = "worker-1", Address = "Test", Gender = "Other" });
			_context.Groups.Add(new GroupOfServices { Id = 1, Name = "Hair" });
			_context.Services.Add(new Service { Id = 1, Name = "Cut", Price = 25 });
			_context.Services_Groups.Add(new ServiceGroup { GroupId = 1, ServiceId = 1 });
			_context.Workers_Groups.Add(new WorkerGroup { GroupId = 1, WorkerId = "worker-1" });
			_context.Schedules.Add(new Schedule
			{
				WorkerId = "worker-1",
				Day = DayOfWeek.Monday.ToString(),
				Start = new TimeSpan(9, 0, 0),
				End = new TimeSpan(17, 0, 0)
			});
			_context.SaveChanges();
		}

		private static DateTime Next(DayOfWeek day)
		{
			var date = DateTime.Today.AddDays(1);
			while (date.DayOfWeek != day) date = date.AddDays(1);
			return date;
		}
	}
}
