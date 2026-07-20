using hhSalon.Domain.Entities;
using hhSalon.Services.Services.Implementations;
using hhSalon.Services.ViewModels;
using hhSalonAPI.Controllers;
using hhSalonAPI.Domain.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hhSalon.Tests.Controllers
{
	public class ServicesControllerTests
	{
		private AppDbContext _context = null!;
		private ServicesController _controller = null!;

		[SetUp]
		public void Setup()
		{
			var options = new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase(Guid.NewGuid().ToString())
				.Options;
			_context = new AppDbContext(options);
			_context.Groups.AddRange(
				new GroupOfServices { Id = 1, Name = "Group 1" },
				new GroupOfServices { Id = 2, Name = "Group 2" });
			_context.Services.Add(new Service { Id = 1, Name = "Service 1", Price = 10 });
			_context.Services_Groups.Add(new ServiceGroup { ServiceId = 1, GroupId = 1 });
			_context.SaveChanges();
			_controller = new ServicesController(new ServicesService(_context));
		}

		[TearDown]
		public void TearDown()
		{
			_context.Database.EnsureDeleted();
			_context.Dispose();
		}

		[Test]
		public async Task GetServicesByGroup_ReturnsExpectedService()
		{
			var result = await _controller.GetServicesVMsByGroupId(1) as OkObjectResult;
			var services = result?.Value as List<ServiceVM>;

			Assert.That(result, Is.Not.Null);
			Assert.That(services, Has.Count.EqualTo(1));
			Assert.That(services![0].Name, Is.EqualTo("Service 1"));
		}

		[Test]
		public async Task CreateService_DoesNotDependOnAnotherTest()
		{
			var result = await _controller.CreateService(new ServiceVM
			{
				GroupId = 2,
				Name = "Service 2",
				Price = 20
			});

			Assert.That(result, Is.TypeOf<OkObjectResult>());
			Assert.That(_context.Services.Count(), Is.EqualTo(2));
		}
	}
}
