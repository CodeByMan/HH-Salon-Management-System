using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using hhSalonAPI.Domain.Concrete;

#nullable disable

namespace hhSalon.Domain.Migrations
{
	[DbContext(typeof(AppDbContext))]
	[Migration("20260716000100_SecurityAndIntegrityHardening")]
	public partial class SecurityAndIntegrityHardening : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(name: "FK_Attendances_Workers_worker_id", table: "Attendances");

			// Raw legacy session/reset tokens cannot safely be converted to hashes; invalidate them.
			migrationBuilder.Sql("UPDATE users SET refresh_token = NULL, reset_password_token = NULL, refresh_token_exp_time = '1970-01-01 00:00:00', reset_password_expiry = '1970-01-01 00:00:00';");

			migrationBuilder.AlterColumn<TimeSpan>(
				name: "time", table: "Attendances", type: "time",
				nullable: false, oldClrType: typeof(TimeSpan), oldType: "time", oldNullable: true);

			migrationBuilder.AlterColumn<decimal>(
				name: "price", table: "Attendances", type: "decimal(10,2)", precision: 10, scale: 2,
				nullable: false, oldClrType: typeof(double), oldType: "double");
			migrationBuilder.AlterColumn<decimal>(
				name: "price", table: "Services", type: "decimal(10,2)", precision: 10, scale: 2,
				nullable: false, oldClrType: typeof(double), oldType: "double");

			migrationBuilder.AlterColumn<string>(
				name: "user_name", table: "users", type: "varchar(100)", maxLength: 100,
				nullable: false, oldClrType: typeof(string), oldType: "longtext", oldNullable: true);
			migrationBuilder.AlterColumn<string>(
				name: "email", table: "users", type: "varchar(254)", maxLength: 254,
				nullable: false, oldClrType: typeof(string), oldType: "longtext", oldNullable: true);
			migrationBuilder.AlterColumn<string>(
				name: "refresh_token", table: "users", type: "varchar(64)", maxLength: 64,
				nullable: true, oldClrType: typeof(string), oldType: "longtext", oldNullable: true);
			migrationBuilder.AlterColumn<string>(
				name: "reset_password_token", table: "users", type: "varchar(64)", maxLength: 64,
				nullable: true, oldClrType: typeof(string), oldType: "longtext", oldNullable: true);

			migrationBuilder.AddColumn<string>(
				name: "payment_transaction_id", table: "Attendances", type: "varchar(32)", nullable: true);

			migrationBuilder.CreateTable(
				name: "PaymentTransactions",
				columns: table => new
				{
					id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
					provider_order_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
					client_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
					attendance_ids = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
					amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
					currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
					status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
					payer_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
					payer_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true),
					created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
					captured_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_PaymentTransactions", x => x.id);
					table.ForeignKey("FK_PaymentTransactions_users_client_id", x => x.client_id, "users", "id", onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.DropIndex(name: "IX_Attendances_ClientId_Date_ServiceId", table: "Attendances");
			migrationBuilder.CreateIndex(name: "UX_Attendances_Client_Date_Service", table: "Attendances", columns: new[] { "client_id", "date", "service_id" }, unique: true);
			migrationBuilder.CreateIndex(name: "UX_Attendances_Worker_Date_Time", table: "Attendances", columns: new[] { "worker_id", "date", "time" }, unique: true);
			migrationBuilder.CreateIndex(name: "IX_Attendances_payment_transaction_id", table: "Attendances", column: "payment_transaction_id");
			migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_client_id", table: "PaymentTransactions", column: "client_id");
			migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_provider_order_id", table: "PaymentTransactions", column: "provider_order_id", unique: true);
			migrationBuilder.CreateIndex(name: "IX_users_email", table: "users", column: "email", unique: true);
			migrationBuilder.CreateIndex(name: "IX_users_user_name", table: "users", column: "user_name", unique: true);

			migrationBuilder.AddForeignKey(
				name: "FK_Attendances_PaymentTransactions_payment_transaction_id",
				table: "Attendances", column: "payment_transaction_id",
				principalTable: "PaymentTransactions", principalColumn: "id", onDelete: ReferentialAction.SetNull);
			migrationBuilder.AddForeignKey(
				name: "FK_Attendances_Workers_worker_id",
				table: "Attendances", column: "worker_id",
				principalTable: "Workers", principalColumn: "id", onDelete: ReferentialAction.Restrict);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(name: "FK_Attendances_Workers_worker_id", table: "Attendances");
			migrationBuilder.DropForeignKey(name: "FK_Attendances_PaymentTransactions_payment_transaction_id", table: "Attendances");
			migrationBuilder.DropTable(name: "PaymentTransactions");
			migrationBuilder.DropIndex(name: "UX_Attendances_Worker_Date_Time", table: "Attendances");
			migrationBuilder.DropIndex(name: "UX_Attendances_Client_Date_Service", table: "Attendances");
			migrationBuilder.DropIndex(name: "IX_users_email", table: "users");
			migrationBuilder.DropIndex(name: "IX_users_user_name", table: "users");
			migrationBuilder.DropColumn(name: "payment_transaction_id", table: "Attendances");

			migrationBuilder.AlterColumn<TimeSpan>(name: "time", table: "Attendances", type: "time", nullable: true, oldClrType: typeof(TimeSpan), oldType: "time");
			migrationBuilder.AlterColumn<double>(name: "price", table: "Attendances", type: "double", nullable: false, oldClrType: typeof(decimal), oldType: "decimal(10,2)", oldPrecision: 10, oldScale: 2);
			migrationBuilder.AlterColumn<double>(name: "price", table: "Services", type: "double", nullable: false, oldClrType: typeof(decimal), oldType: "decimal(10,2)", oldPrecision: 10, oldScale: 2);
			migrationBuilder.AlterColumn<string>(name: "user_name", table: "users", type: "longtext", nullable: true, oldClrType: typeof(string), oldType: "varchar(100)", oldMaxLength: 100, oldNullable: true);
			migrationBuilder.AlterColumn<string>(name: "email", table: "users", type: "longtext", nullable: true, oldClrType: typeof(string), oldType: "varchar(254)", oldMaxLength: 254, oldNullable: true);
			migrationBuilder.AlterColumn<string>(name: "refresh_token", table: "users", type: "longtext", nullable: true, oldClrType: typeof(string), oldType: "varchar(64)", oldMaxLength: 64, oldNullable: true);
			migrationBuilder.AlterColumn<string>(name: "reset_password_token", table: "users", type: "longtext", nullable: true, oldClrType: typeof(string), oldType: "varchar(64)", oldMaxLength: 64, oldNullable: true);
			migrationBuilder.CreateIndex(name: "IX_Attendances_ClientId_Date_ServiceId", table: "Attendances", columns: new[] { "client_id", "date", "service_id" }, unique: true);
			migrationBuilder.AddForeignKey(
				name: "FK_Attendances_Workers_worker_id",
				table: "Attendances", column: "worker_id",
				principalTable: "Workers", principalColumn: "id", onDelete: ReferentialAction.Cascade);
		}
	}
}
