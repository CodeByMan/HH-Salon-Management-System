using hhSalon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hhSalon.Domain.Concrete.EntityConfiguration
{
	public class UserConfiguration : IEntityTypeConfiguration<User>
	{
		public void Configure(EntityTypeBuilder<User> builder)
		{
			builder.HasIndex(user => user.UserName).IsUnique();
			builder.HasIndex(user => user.Email).IsUnique();
			builder.Property(user => user.UserName).IsRequired().HasMaxLength(100);
			builder.Property(user => user.Email).IsRequired().HasMaxLength(254);
			builder.Property(user => user.RefreshTokenHash).HasMaxLength(64);
			builder.Property(user => user.ResetPasswordTokenHash).HasMaxLength(64);
		}
	}
}
