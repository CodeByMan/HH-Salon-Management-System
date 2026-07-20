using hhSalon.Domain.Entities;
using hhSalon.Services.Models.Dto;

namespace hhSalon.Services.Services.Interfaces
{
	public interface IUsersService
	{
		Task<IEnumerable<UserDto>> GetAllUser();
		User GetUserById(string id);
		Task UpdateUser(UserUpdateDto userObj);
	}
}
