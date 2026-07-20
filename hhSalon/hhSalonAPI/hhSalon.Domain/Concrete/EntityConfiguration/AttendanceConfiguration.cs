using hhSalon.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace hhSalon.Domain.Concrete.EntityConfiguration
{
	public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
	{
		public void Configure(EntityTypeBuilder<Attendance> builder)
		{
			builder.HasIndex(att => new { att.ClientId, att.Date, att.ServiceId }).IsUnique().HasDatabaseName("UX_Attendances_Client_Date_Service");
			builder.HasIndex(att => new { att.WorkerId, att.Date, att.Time }).IsUnique().HasDatabaseName("UX_Attendances_Worker_Date_Time");
			builder.Property(att => att.Time).IsRequired().HasColumnType("time");
			builder.Property(att => att.PaymentTransactionId).IsConcurrencyToken();
			builder.Property(att => att.Date).HasColumnType("date");
			builder.Property(att => att.Price).HasPrecision(10, 2);
			builder.HasOne(att => att.PaymentTransaction)
				.WithMany(payment => payment.Attendances)
				.HasForeignKey(att => att.PaymentTransactionId)
				.OnDelete(DeleteBehavior.SetNull);
		}
	}
}
