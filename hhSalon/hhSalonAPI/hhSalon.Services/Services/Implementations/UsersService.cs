using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;
using hhSalon.Services.Services.Interfaces;
using hhSalonAPI.Domain.Concrete;
using Microsoft.EntityFrameworkCore;

namespace hhSalon.Services.Services.Implementations
{
	public class UsersService : IUsersService
	{
		private readonly AppDbContext _context;

		public UsersService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<IEnumerable<UserDto>> GetAllUser()
		{
			return await _context.Users
				.AsNoTracking()
				.Select(user => new UserDto
				{
					Id = user.Id,
					FirstName = user.FirstName,
					LastName = user.LastName,
					Email = user.Email,
					UserName = user.UserName
				})
				.ToListAsync();
		}

		public User GetUserById(string id)
		{
			return _context.Users.AsNoTracking().FirstOrDefault(user => user.Id == id);
		}

		public async Task UpdateUser(UserUpdateDto userObj)
		{
			var duplicateEmail = await _context.Users.AnyAsync(user => user.Email == userObj.Email && user.Id != userObj.Id);
			if (duplicateEmail)
				throw new InvalidOperationException("This email is already taken.");

			var user = await _context.Users.FirstOrDefaultAsync(existingUser => existingUser.Id == userObj.Id);
			if (user == null)
				throw new KeyNotFoundException("User not found.");

			user.FirstName = userObj.FirstName;
			user.LastName = userObj.LastName;
			user.Email = userObj.Email;

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				throw new InvalidOperationException("The account could not be updated because the email is already in use.", ex);
			}
		}
	}
}
