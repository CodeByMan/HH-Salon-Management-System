using hhSalon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hhSalon.Domain.Concrete.EntityConfiguration
{
	public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
	{
		public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
		{
			builder.HasIndex(payment => payment.ProviderOrderId).IsUnique();
			builder.Property(payment => payment.ProviderOrderId).HasMaxLength(64);
			builder.Property(payment => payment.AttendanceIds).HasMaxLength(1000);
			builder.Property(payment => payment.Id).HasMaxLength(32);
			builder.Property(payment => payment.ClientId).HasMaxLength(255);
			builder.Property(payment => payment.Status).HasMaxLength(32);
			builder.Property(payment => payment.Currency).HasMaxLength(3);
			builder.Property(payment => payment.PayerId).HasMaxLength(64);
			builder.Property(payment => payment.PayerEmail).HasMaxLength(254);
			builder.Property(payment => payment.Amount).HasPrecision(10, 2);
			builder.HasOne(payment => payment.Client)
				.WithMany(user => user.PaymentTransactions)
				.HasForeignKey(payment => payment.ClientId)
				.OnDelete(DeleteBehavior.Restrict);
		}
	}
}
