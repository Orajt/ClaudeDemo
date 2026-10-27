using Microsoft.EntityFrameworkCore;

namespace Spike.DataHub;

/// <summary>Widok stanu: jeden wiersz na zamówienie, połówki Orders i Settlements z własnymi wersjami (D-006).</summary>
public sealed class OrderPaymentRow
{
    public Guid OrderId { get; set; }

    public string? CustomerName { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? OrderStatus { get; set; }
    public long OrdersVersion { get; set; }

    public string? SettlementStatus { get; set; }
    public decimal? SettledNetAmount { get; set; }
    public long SettlementVersion { get; set; }

    /// <summary>Token współbieżności – kolumna systemowa xmin.</summary>
    public uint RowVersion { get; set; }
}

public sealed class DataHubDbContext(DbContextOptions<DataHubDbContext> options) : DbContext(options)
{
    public const string SchemaName = "spike_datahub";

    public DbSet<OrderPaymentRow> OrderPayments => Set<OrderPaymentRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<OrderPaymentRow>(row =>
        {
            row.ToTable("order_payments");
            row.HasKey(x => x.OrderId);
            row.Property(x => x.OrderId).HasColumnName("order_id");
            row.Property(x => x.CustomerName).HasColumnName("customer_name");
            row.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            row.Property(x => x.OrderStatus).HasColumnName("order_status");
            row.Property(x => x.OrdersVersion).HasColumnName("orders_version");
            row.Property(x => x.SettlementStatus).HasColumnName("settlement_status");
            row.Property(x => x.SettledNetAmount).HasColumnName("settled_net_amount").HasPrecision(18, 2);
            row.Property(x => x.SettlementVersion).HasColumnName("settlement_version");
            row.Property(x => x.RowVersion).IsRowVersion();
        });
    }
}
