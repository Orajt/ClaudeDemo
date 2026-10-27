using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spike.DataHub.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spike_datahub");

            migrationBuilder.CreateTable(
                name: "order_payments",
                schema: "spike_datahub",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "text", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    order_status = table.Column<string>(type: "text", nullable: true),
                    orders_version = table.Column<long>(type: "bigint", nullable: false),
                    settlement_status = table.Column<string>(type: "text", nullable: true),
                    settled_net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    settlement_version = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payments", x => x.order_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_payments",
                schema: "spike_datahub");
        }
    }
}
